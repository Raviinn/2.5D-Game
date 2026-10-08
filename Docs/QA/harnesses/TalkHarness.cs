using System;
using System.Collections;
using System.IO;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: NPCs say your name and react to night, rain, wounds and gear (Milestone 52). Runs with -talkharness.</summary>
public sealed class TalkHarness : MonoBehaviour
{
    string dir;
    int failures;
    GameObject player;
    DialogueRunner runner;
    PlayerMotor motor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-talkharness") < 0) return;
        var go = new GameObject("TalkHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<TalkHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[TalkTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return Wait(3f);

        player = GameObject.FindWithTag("Player");
        motor = player.GetComponent<PlayerMotor>();
        runner = Services.Get<DialogueRunner>();
        var me = player.GetComponent<Combatant>();
        var weather = Services.Get<WeatherSystem>();
        var clock = Services.Get<WorldClock>();
        foreach (var e in EnemyController.Active.ToArray()) e.enabled = false;

        var appearance = player.GetComponent<PlayerAppearance>();
        var look = appearance.Appearance;
        look.name = "Edda";
        appearance.SetAppearance(look);
        Check(appearance.PlayerName == "Edda", "the hero is called Edda");

        var oswin = GameObject.Find("Merchant");
        var brenna = GameObject.Find("Blacksmith");
        var maren = GameObject.Find("Healer");

        // First meetings: Brenna and Maren ask your name and use it.
        string text = null;
        yield return Lines(brenna, s => text = s);
        Check(text.Contains("Edda"), $"Brenna learns your name (\"{Find(text, "Edda")}\")");
        yield return Lines(maren, s => text = s);
        Check(text.Contains("Edda"), $"Maren too (\"{Find(text, "Edda")}\")");

        // Wounded: Maren and Oswin notice.
        yield return Lines(oswin, s => text = s); // Oswin's introduction
        me.ReceiveWorldDamage(me.MaxHealth * 0.7f);
        yield return Lines(maren, s => text = s);
        Check(text.Contains("bleeding"), $"hurt: Maren notices (\"{Find(text, "bleeding")}\")");
        yield return Lines(oswin, s => text = s);
        Check(text.Contains("bleeding") && text.Contains("Edda"), $"and so does Oswin, by name (\"{Find(text, "bleeding")}\")");
        me.Heal(999f);

        // Rain.
        weather.Set(Weather.Rain);
        yield return Wait(0.3f);
        yield return Lines(maren, s => text = s);
        Check(text.Contains("wet"), $"rain: Maren calls you in (\"{Find(text, "wet")}\")");
        weather.Set(Weather.Clear);

        // Broken gear: Brenna notices.
        var gear = player.GetComponent<GearCondition>();
        var equipment = player.GetComponent<PlayerEquipment>();
        gear.Wear(equipment.ActiveWeapon, 9999f);
        yield return Lines(brenna, s => text = s);
        Check(text.Contains("pieces"), $"broken gear: Brenna offers to mend it (\"{Find(text, "pieces")}\")");
        gear.Repair(equipment.ActiveWeapon);

        // Night.
        double now = clock.TotalMinutes % WorldClock.MinutesPerDay;
        clock.AdvanceMinutes((23 * 60 - now + WorldClock.MinutesPerDay) % WorldClock.MinutesPerDay);
        yield return Wait(0.5f);
        yield return Lines(oswin, s => text = s);
        Check(text.Contains("Late") || text.Contains("night"), $"night: Oswin remarks on the hour (\"{text.Trim()}\")");
        Finish();
    }

    /// <summary>Talks to an NPC and collects every line until the first choices, then leaves.</summary>
    IEnumerator Lines(GameObject npc, Action<string> collected)
    {
        var schedule = npc.GetComponent<NpcSchedule>();
        if (schedule != null) schedule.enabled = false; // they stay put (and awake) for the test
        npc.GetComponent<Interactable>().Unavailable = false;
        motor.Teleport(npc.transform.position + npc.transform.forward * 2f + Vector3.up * 0.1f, Quaternion.LookRotation(-npc.transform.forward));
        yield return Wait(0.3f);
        npc.GetComponent<DialogueSpeaker>().Interact(player.GetComponent<PlayerInteractor>());
        var all = new System.Text.StringBuilder();
        float end = Time.realtimeSinceStartup + 5f;
        while (runner.IsActive && Time.realtimeSinceStartup < end)
        {
            all.AppendLine(runner.CurrentText);
            if (runner.Choices.Count > 0) break;
            runner.Advance();
            yield return null;
        }
        collected(all.ToString());
        Services.Get<GameStateService>().SetState(GameState.Playing);
        yield return Wait(0.2f);
    }

    static string Find(string text, string word)
    {
        foreach (var line in text.Split('\n')) if (line.Contains(word)) return line.Trim();
        return text.Trim();
    }

    void Finish()
    {
        Debug.Log($"[TalkTest] done, failures={failures}");
        Application.Quit();
    }

    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);
}
