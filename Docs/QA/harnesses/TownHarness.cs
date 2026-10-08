using System;
using System.Collections;
using System.IO;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: NPC daily routines (Milestone 31). Runs only with -townharness.</summary>
public sealed class TownHarness : MonoBehaviour
{
    string dir;
    int failures;
    // Out of everyone's way: away from the bandit camp and (since Milestone 40) the wolf den in the north-west woods.
    static readonly Vector3 Parking = new(-2f, 1.1f, 22f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-townharness") < 0) return;
        var go = new GameObject("TownHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<TownHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[TownTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return new WaitForSecondsRealtime(3f);

        var player = GameObject.FindWithTag("Player");
        var motor = player.GetComponent<PlayerMotor>();
        var clock = Services.Get<WorldClock>();
        var oswin = GameObject.Find("Merchant")?.GetComponent<NpcSchedule>();
        var brenna = GameObject.Find("Blacksmith")?.GetComponent<NpcSchedule>();
        Check(oswin != null && oswin.EntryCount >= 5, $"Oswin has a routine ({oswin?.EntryCount} parts)");
        Check(brenna != null && brenna.EntryCount >= 5, $"Brenna has a routine ({brenna?.EntryCount} parts)");
        if (oswin == null || brenna == null) { Finish(); yield break; }

        // Park the player out of the way (NPCs stop walking when you're next to them).
        motor.Teleport(Parking, Quaternion.identity);

        // ---- Daytime: at work ----
        GoTo(clock, 10f);
        yield return Wait(0.5f);
        Check(oswin.Activity == "At the stall" && At(oswin, 0) && !oswin.IsIndoors, $"10:00 Oswin is at his stall ({oswin.Activity}, {Off(oswin, 0):0.0} m off)");
        Check(brenna.Activity == "At the forge" && At(brenna, 0), $"10:00 Brenna is at the forge ({brenna.Activity})");

        // ---- Noon: Oswin walks to the well ----
        GoTo(clock, 11f + 58f / 60f);
        yield return Wait(0.3f);
        bool walked = false, ranClip = false;
        float start = Time.realtimeSinceStartup;
        var oswinController = oswin.GetComponent<NpcController>();
        bool shot = false;
        while (Time.realtimeSinceStartup - start < 30f && !(walked && !oswin.IsWalking))
        {
            walked |= oswin.IsWalking;
            ranClip |= oswinController.CurrentAnim == CharacterAnim.Run;
            if (oswin.IsWalking && !shot && Time.realtimeSinceStartup - start > 3f)
            {
                shot = true;
                motor.Teleport(oswin.transform.position + new Vector3(0f, 0.1f, -6f), Quaternion.identity);
                yield return Wait(0.2f);
                yield return Shot("t1_walking");
                motor.Teleport(Parking, Quaternion.identity);
            }
            yield return null;
        }
        int well = IndexOf(oswin, "Lunch at the well");
        Check(walked && ranClip, "at noon Oswin walks (run clip, slowed)");
        Check(oswin.Activity == "Lunch at the well" && At(oswin, well), $"and reaches the well ({Off(oswin, well):0.0} m off) in {Time.realtimeSinceStartup - start:0} s");

        // ---- He stops for the player ----
        GoTo(clock, 12f + 58f / 60f);
        yield return Wait(0.2f);
        motor.Teleport(oswin.transform.position + oswin.transform.forward * 1.2f + Vector3.up * 0.1f, Quaternion.identity);
        yield return Wait(4f);
        Vector3 held = oswin.transform.position;
        yield return Wait(1.5f);
        Check(oswin.IsWalking && (oswin.transform.position - held).magnitude < 0.05f, "he waits while you stand next to him");
        motor.Teleport(Parking, Quaternion.identity);
        yield return Wait(8f);
        Check(oswin.Activity == "At the stall", $"then carries on ({oswin.Activity})");

        // ---- Night: both asleep at home ----
        GoTo(clock, 23f);
        yield return Wait(0.5f);
        var oswinTalk = oswin.GetComponent<Interactable>();
        Check(oswin.IsIndoors && brenna.IsIndoors, "23:00 Oswin and Brenna are asleep indoors");
        Check(!oswin.GetComponentInChildren<Renderer>().enabled && oswinTalk.Unavailable, "asleep: hidden and can't be talked to");
        motor.Teleport(oswin.EntryAt(IndexOf(oswin, "Asleep at home")).Place.position + new Vector3(0f, 1.1f, -3f), Quaternion.identity);
        yield return Wait(0.5f);
        var interactor = player.GetComponent<PlayerInteractor>();
        Check(interactor.Focus == null || interactor.Focus.gameObject != oswin.gameObject, "no prompt to talk to a sleeping Oswin at his door");
        yield return Shot("t2_night_door");

        // ---- Morning: back at work ----
        GoTo(clock, 6.5f);
        yield return Wait(0.5f);
        Check(!oswin.IsIndoors && !brenna.IsIndoors && At(oswin, 0) && At(brenna, 0) && !oswinTalk.Unavailable,
            $"06:30 both are back at work ({oswin.Activity}, {brenna.Activity})");

        // ---- Brenna reads the board at 13:00 ----
        GoTo(clock, 13.4f);
        yield return Wait(0.5f);
        int boardIndex = IndexOf(brenna, "Reading the contract board");
        Check(brenna.Activity == "Reading the contract board" && At(brenna, boardIndex), $"13:24 Brenna is at the contract board ({Off(brenna, boardIndex):0.0} m off)");

        yield return MusicTests(clock, motor);
        Finish();
    }

    // ---------- Milestone 33: music ----------

    IEnumerator MusicTests(WorldClock clock, PlayerMotor motor)
    {
        var audio = GameAudio.Instance;
        var library = audio != null ? audio.Library : null;
        Check(library != null && library.MusicDay != null && library.MusicNight != null && library.MusicCombat != null && library.MusicMenu != null,
            "four music tracks in the sound library");
        if (library == null) yield break;
        string Now() => audio.CurrentMusic != null ? audio.CurrentMusic.name : "none";

        // Earlier steps (night at the houses) may have drawn bandits into town: send everyone home first.
        motor.Teleport(Parking, Quaternion.identity);
        foreach (var e in EnemyController.Active) e.Warp(e.Home, true);
        GoTo(clock, 10f);
        yield return Wait(7f);
        Check(audio.CurrentMusic == library.MusicDay && audio.MusicVolumeNow > 0.1f, $"10:00 the day music plays ({Now()}, volume {audio.MusicVolumeNow:0.00})");

        GoTo(clock, 23f);
        yield return Wait(5f);
        Check(audio.CurrentMusic == library.MusicNight, $"23:00 the night music plays ({Now()})");

        // Bandits chasing you: combat music. Walk away: back to the night theme after a few seconds.
        GoTo(clock, 10f);
        yield return Wait(1f);
        EnemyController bandit = null;
        foreach (var e in EnemyController.Active) if (!e.Data.IsTrainingDummy && !e.Data.Ranged) { bandit = e; break; }
        var me = motor.GetComponent<Combatant>();
        me.Invulnerable = true;
        motor.Teleport(bandit.Home + new Vector3(3f, 0.1f, 0f), Quaternion.identity);
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 8f && audio.CurrentMusic != library.MusicCombat) yield return null;
        Check(audio.InCombat && audio.CurrentMusic == library.MusicCombat, $"a bandit chasing you starts the combat music in {Time.realtimeSinceStartup - start:0.0} s ({Now()})");
        yield return Wait(1.5f);
        yield return Shot("t3_combat");
        motor.Teleport(Parking, Quaternion.identity);
        start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 20f && audio.CurrentMusic == library.MusicCombat) yield return null;
        Check(!audio.InCombat && audio.CurrentMusic == library.MusicDay, $"after the fight the day music returns ({Time.realtimeSinceStartup - start:0} s, {Now()})");
        me.Invulnerable = false;

        // Settings → Audio → Music at 0 silences it.
        var settings = Services.Get<SettingsService>();
        float before = settings.Current.musicVolume;
        settings.Current.musicVolume = 0f;
        settings.Apply();
        yield return Wait(3f);
        Check(audio.MusicVolumeNow < 0.01f, $"Music volume 0 silences the music ({audio.MusicVolumeNow:0.000})");
        settings.Current.musicVolume = before;
        settings.Apply();
    }

    /// <summary>Moves the clock forward to this hour (a jump, so NPCs are placed straight at their spots).</summary>
    static void GoTo(WorldClock clock, float hour)
    {
        double now = clock.TotalMinutes % WorldClock.MinutesPerDay;
        double target = hour * 60.0;
        double delta = target - now;
        if (delta <= 0) delta += WorldClock.MinutesPerDay;
        clock.AdvanceMinutes(delta);
    }

    static int IndexOf(NpcSchedule schedule, string activity)
    {
        for (int i = 0; i < schedule.EntryCount; i++) if (schedule.EntryAt(i).Activity == activity) return i;
        return 0;
    }

    static float Off(NpcSchedule schedule, int entry)
    {
        var place = schedule.EntryAt(entry).Place;
        if (place == null) return 999f;
        var d = schedule.transform.position - place.position;
        d.y = 0f;
        return d.magnitude;
    }

    static bool At(NpcSchedule schedule, int entry) => Off(schedule, entry) < 0.6f;

    void Finish()
    {
        Debug.Log($"[TownTest] done, failures={failures}");
        Application.Quit();
    }

    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }
}
