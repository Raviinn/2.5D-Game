using System;
using System.Collections;
using System.IO;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test-only: the conversation camera (Milestone 36), the chest off the farm field (37) and the minimap trail (38).
/// Runs only with -cinemaharness.
/// </summary>
public sealed class CinemaHarness : MonoBehaviour
{
    string dir;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-cinemaharness") < 0) return;
        var go = new GameObject("CinemaHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<CinemaHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[CinemaTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return Wait(3f);

        var player = GameObject.FindWithTag("Player");
        var motor = player.GetComponent<PlayerMotor>();
        player.GetComponent<Combatant>().Invulnerable = true;
        foreach (var e in EnemyController.Active) e.Warp(e.Home, true);

        yield return ChestTests(player, motor);
        yield return ConversationTests(player, motor);
        yield return TrailTests(player, motor);
        Finish();
    }

    // ---------- Milestone 37: the chest stands off the field ----------

    IEnumerator ChestTests(GameObject player, PlayerMotor motor)
    {
        var chest = FindFirstObjectByType<StorageChest>();
        var plot = FindFirstObjectByType<FarmPlot>();
        Check(chest != null && plot != null, "the storage chest and the farm field exist");
        if (chest == null || plot == null) yield break;
        Vector3 offset = chest.transform.position - plot.WorldCenter;
        bool off = Mathf.Abs(offset.x) > 3.7f || Mathf.Abs(offset.z) > 3.7f;
        Check(chest.transform.position.y < 0.3f, $"the chest stands on the ground (y {chest.transform.position.y:0.00})");
        var bedSpot = FindFirstObjectByType<SleepSpot>();
        float fromBed = bedSpot != null ? Vector3.Distance(Flat(bedSpot.transform.position), Flat(chest.transform.position)) : 0f;
        Check(fromBed < 3.5f, $"and close to the bed ({fromBed:0.0} m)");
        Check(off, $"the chest stands off the field ({chest.transform.position:F1}, field centre {plot.WorldCenter:F1})");

        // Walk-up check: standing next to it, facing it, the chest is what F opens.
        Vector3 toward = chest.transform.forward;
        motor.Teleport(chest.transform.position + toward * 1.3f + Vector3.up * 1.1f, Quaternion.LookRotation(-toward));
        yield return Wait(0.6f);
        var interactor = player.GetComponent<PlayerInteractor>();
        Check(interactor.Focus == chest, $"walking up to it offers the chest ({(interactor.Focus != null ? interactor.Focus.name : "nothing")})");
        yield return Shot("c0_chest");
    }

    // ---------- Milestone 36: conversation camera ----------

    IEnumerator ConversationTests(GameObject player, PlayerMotor motor)
    {
        var cam = Camera.main;
        var rig = cam.GetComponent<ThirdPersonCamera>();
        var oswin = GameObject.Find("Merchant");
        var speaker = oswin != null ? oswin.GetComponent<DialogueSpeaker>() : null;
        var runner = Services.Get<DialogueRunner>();
        Check(rig != null && speaker != null, "camera rig and Oswin found");
        if (rig == null || speaker == null) yield break;

        // Stand 2 m from him, looking away, so both have to turn.
        Vector3 spot = oswin.transform.position + oswin.transform.forward * 2f;
        motor.Teleport(spot + Vector3.up * 0.1f, Quaternion.LookRotation(oswin.transform.forward));
        yield return Wait(1.5f);
        float fovBefore = cam.fieldOfView;

        speaker.Interact(player.GetComponent<PlayerInteractor>());
        Check(runner.IsActive && runner.ConversationPartner == oswin.transform, "talking to Oswin starts a conversation with him as the partner");
        Vector3 between = oswin.transform.position - player.transform.position;
        between.y = 0f;
        Check(Vector3.Dot(player.transform.forward, between.normalized) > 0.97f, "the player turns to face Oswin at once");
        Check(Vector3.Dot(oswin.transform.forward, -between.normalized) > 0.97f, "Oswin turns to face the player at once");

        // Watch the blend frame by frame: it should pass through in-between values over about 0.7 s.
        float began = Time.realtimeSinceStartup, settled = -1f;
        int glideFrames = 0;
        while (Time.realtimeSinceStartup - began < 2f && settled < 0f)
        {
            if (rig.ConversationBlend > 0.05f && rig.ConversationBlend < 0.95f) glideFrames++;
            if (rig.ConversationBlend >= 1f) settled = Time.realtimeSinceStartup - began;
            yield return null;
        }
        Check(glideFrames >= 5 && settled > 0.4f, $"the camera glides in ({glideFrames} in-between frames, settled after {settled:0.00} s)");
        Check(rig.ConversationBlend >= 1f, "and settles into the conversation shot");
        yield return Wait(0.3f);
        // Heads are about 1.4 m above the feet (transforms sit at the middle of the colliders).
        float oswinFeet = oswin.GetComponent<Collider>().bounds.min.y;
        float playerFeet = player.GetComponent<CharacterController>().bounds.min.y;
        Vector3 oswinHead = cam.WorldToViewportPoint(new Vector3(oswin.transform.position.x, oswinFeet + 1.4f, oswin.transform.position.z));
        Vector3 playerHead = cam.WorldToViewportPoint(new Vector3(player.transform.position.x, playerFeet + 1.4f, player.transform.position.z));
        Vector3 oswinFoot = cam.WorldToViewportPoint(new Vector3(oswin.transform.position.x, oswinFeet + 0.2f, oswin.transform.position.z));
        Check(oswinHead.z > 0f && oswinHead.x > 0.4f && oswinHead.x < 0.75f && oswinHead.y > 0.4f && oswinHead.y < 0.85f,
            $"Oswin is framed in the middle of the shot (viewport {oswinHead.x:0.00}, {oswinHead.y:0.00})");
        Check(oswinFoot.y > 0.2f, $"Oswin stands above the dialogue text (feet at viewport y {oswinFoot.y:0.00})");
        Check(playerHead.z > 0f && playerHead.x < oswinHead.x - 0.1f, $"the player is in the left foreground (viewport x {playerHead.x:0.00})");
        Check(playerHead.z < oswinHead.z, "the player is nearer the camera than Oswin (over the shoulder)");
        Check(cam.fieldOfView < fovBefore - 1f, $"the lens narrows for the shot ({fovBefore:0} → {cam.fieldOfView:0})");
        float lineHeight = cam.transform.position.y - playerFeet;
        Check(lineHeight > 1.3f && lineHeight < 2.1f, $"the camera is at about eye height ({lineHeight:0.00} m)");
        yield return Shot("c1_conversation_line");

        // Mouse / stick look is ignored during the shot.
        var before = cam.transform.rotation;
        yield return Wait(0.3f);
        Check(Quaternion.Angle(before, cam.transform.rotation) < 0.5f, "the shot holds still");

        // Advance to the first set of choices.
        float end = Time.realtimeSinceStartup + 20f;
        while (runner.IsActive && runner.Choices.Count == 0 && Time.realtimeSinceStartup < end)
        {
            runner.Advance();
            yield return Wait(0.4f);
        }
        Check(runner.Choices.Count > 0, $"reached the choices ({runner.Choices.Count})");
        yield return Wait(2f); // the typewriter finishes, choices show
        yield return Shot("c2_conversation_choices");

        // Leaving (Esc) glides back behind the player, looking toward Oswin.
        Services.Get<GameStateService>().SetState(GameState.Playing);
        yield return Wait(0.2f);
        Check(!runner.IsActive, "Esc leaves the conversation");
        yield return Wait(1.2f);
        Check(rig.ConversationBlend <= 0f, "the camera glides back out");
        Check(Mathf.Abs(cam.fieldOfView - fovBefore) < 0.5f, "the lens is back to normal");
        Vector3 look = cam.transform.forward;
        look.y = 0f;
        Check(Vector3.Dot(look.normalized, between.normalized) > 0.9f, "the camera ends up behind the player, facing Oswin");
        yield return Shot("c3_after_conversation");
    }

    // ---------- Milestone 38: trail on the minimap ----------

    IEnumerator TrailTests(GameObject player, PlayerMotor motor)
    {
        var map = FindFirstObjectByType<Minimap>();
        var tracker = FindFirstObjectByType<QuestTracker>();
        var quests = player.GetComponent<QuestLog>();
        Services.Get<GameDatabase>().TryGet("quest_bandittrouble", out QuestData banditTrouble);
        Check(map != null && tracker != null && banditTrouble != null, "minimap, tracker and Bandit Trouble found");
        if (map == null || tracker == null || banditTrouble == null) yield break;

        motor.Teleport(new Vector3(0f, 1.1f, -3f), Quaternion.identity);
        yield return Wait(1f);
        Check(map.Trail.Count == 0, "no trail without a tracked quest");

        quests.StartQuest(banditTrouble);
        yield return Wait(1.5f);
        Check(tracker.HasTarget, "Bandit Trouble is tracked and has a target");
        int count = map.Trail.Count;
        Check(count >= 2, $"a trail leads toward it ({count} points)");
        if (count >= 2)
        {
            float reach = Vector3.Distance(Flat(map.Trail[count - 1]), Flat(tracker.TargetPosition));
            Check(reach < 2.5f, $"the trail ends at the target ({reach:0.0} m off)");
            Check(Vector3.Distance(Flat(map.Trail[0]), Flat(player.transform.position)) < 2f, "and starts at the player");
        }
        yield return Shot("c4_trail_minimap");

        // Behind a house: the route bends around it instead of going straight through.
        var house = GameObject.Find("House_A");
        if (house != null)
        {
            Vector3 target = tracker.TargetPosition;
            Vector3 away = house.transform.position - target;
            away.y = 0f;
            motor.Teleport(house.transform.position + away.normalized * 5f + Vector3.up * 0.1f, Quaternion.identity);
            yield return Wait(1.5f);
            Check(map.Trail.Count >= 3, $"from behind House_A the route bends ({map.Trail.Count} points)");
            yield return Shot("c5_trail_around_house");
        }

        // The large map shows it too.
        var menu = FindFirstObjectByType<GameMenu>();
        menu.Open(GameMenuTab.Map);
        yield return Wait(1f);
        Check(map.Trail.Count >= 2, "the trail is kept on the large map");
        yield return Shot("c6_trail_large_map");
        Services.Get<GameStateService>().SetState(GameState.Playing);
        yield return Wait(0.5f);

        // Target reached: the trail shrinks to nothing as you get there.
        var bandit = tracker.TargetPosition;
        motor.Teleport(bandit + new Vector3(1.5f, 0.2f, 0f), Quaternion.identity);
        yield return Wait(1f);
        float length = 0f;
        for (int i = 1; i < map.Trail.Count; i++) length += Vector3.Distance(map.Trail[i - 1], map.Trail[i]);
        Check(length < 6f, $"standing at the target the trail is short ({length:0.0} m)");
    }

    static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

    void Finish()
    {
        Debug.Log($"[CinemaTest] done, failures={failures}");
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
