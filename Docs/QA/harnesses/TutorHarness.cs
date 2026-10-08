using System;
using System.Collections;
using System.IO;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: the first-day guide, tips and the How to play page (Milestone 53). Runs with -tutorharness.</summary>
public sealed class TutorHarness : MonoBehaviour
{
    string dir;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-tutorharness") < 0) return;
        var go = new GameObject("TutorHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<TutorHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[TutorTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return Wait(3f);

        var player = GameObject.FindWithTag("Player");
        var me = player.GetComponent<Combatant>();
        var motor = player.GetComponent<PlayerMotor>();
        var inventory = player.GetComponent<Inventory>();
        var clock = Services.Get<WorldClock>();
        var guide = FindFirstObjectByType<TutorialGuide>();
        me.Invulnerable = true;
        Check(guide != null, "the first-day guide is in the world");
        if (guide == null) { Finish(); yield break; }
        Check(guide.Current == TutorialGuide.Step.Move && guide.StepTextNow.Contains("WASD"), $"a new game starts with: \"{guide.StepTextNow}\"");
        yield return Shot("t1_first_step");

        motor.Teleport(player.transform.position + new Vector3(8f, 0.1f, 0f), player.transform.rotation);
        yield return Wait(0.3f);
        Check(guide.Current == TutorialGuide.Step.Till && guide.StepTextNow.Contains("[F]"), $"walking a little moves on: \"{guide.StepTextNow}\"");

        EventBus<FarmActionEvent>.Raise(new FarmActionEvent(FarmAction.Till, Vector3.zero));
        Check(guide.Current == TutorialGuide.Step.Plant, "tilling ticks off the next step");
        EventBus<FarmActionEvent>.Raise(new FarmActionEvent(FarmAction.Plant, Vector3.zero));
        EventBus<FarmActionEvent>.Raise(new FarmActionEvent(FarmAction.Water, Vector3.zero));
        Check(guide.Current == TutorialGuide.Step.TalkToOswin, $"then planting and watering ({guide.Current})");

        var oswin = GameObject.Find("Merchant");
        motor.Teleport(oswin.transform.position + oswin.transform.forward * 2f + Vector3.up * 0.1f, Quaternion.LookRotation(-oswin.transform.forward));
        yield return Wait(0.3f);
        oswin.GetComponent<DialogueSpeaker>().Interact(player.GetComponent<PlayerInteractor>());
        yield return Wait(0.3f);
        Services.Get<GameStateService>().SetState(GameState.Playing);
        yield return Wait(0.2f);
        Check(guide.Current == TutorialGuide.Step.Practise, $"talking to Oswin ({guide.Current})");

        EnemyController dummy = null;
        foreach (var e in EnemyController.Active) if (e.Data.IsTrainingDummy) dummy = e;
        for (int i = 0; i < 3; i++) dummy.Combatant.ReceiveHit(new DamageInfo { Damage = 1f, Attacker = me });
        Check(guide.Current == TutorialGuide.Step.Sleep, "three hits on the dummy");
        clock.AdvanceMinutes(WorldClock.MinutesPerDay);
        yield return Wait(0.3f);
        Check(guide.Current == TutorialGuide.Step.Done && guide.Tip != null && guide.Tip.Contains("How to play"), $"a new day: done (\"{guide.Tip}\")");

        // One-off tips afterwards, one at a time (the seeds tip may come first: you start with two kinds).
        yield return Wait(8.5f);
        if (guide.Tip != null) yield return Wait(8.5f);
        Services.Get<GameDatabase>().TryGet("item_fishingrod", out ItemData rod);
        inventory.Add(rod, 1);
        yield return Wait(0.3f);
        Check(guide.Tip != null && guide.Tip.Contains("Fishing"), $"the first fishing rod brings a tip (\"{guide.Tip}\")");
        yield return Shot("t2_tip");

        // Rebound keys show in the guide.
        int interactRow = Array.FindIndex(KeyBindings.Rows, r => r.Action == "Interact");
        KeyBindings.Bind(interactRow, false, "<Keyboard>/g");
        guide.SetStep(TutorialGuide.Step.Till);
        Check(guide.StepTextNow.Contains("[G]"), $"rebound keys show in the guide: \"{guide.StepTextNow}\"");
        KeyBindings.ResetAll();

        // Saved per slot.
        var save = Services.Get<SaveService>();
        save.Save();
        yield return Wait(0.4f);
        guide.SetStep(TutorialGuide.Step.Done);
        save.Load();
        yield return Wait(1.5f);
        Check(FindFirstObjectByType<TutorialGuide>().Current == TutorialGuide.Step.Till, "the guide's progress is saved with the game");

        // Settings → Interface → Tutorial hints.
        var settings = Services.Get<SettingsService>();
        settings.Current.tutorialHints = false;
        settings.Apply();
        Check(!guide.Enabled, "hints can be turned off in Settings");
        settings.Current.tutorialHints = true;
        settings.Apply();
        settings.Save();

        // How to play.
        var menu = FindFirstObjectByType<GameMenu>();
        menu.Open(GameMenuTab.Options);
        yield return Wait(0.3f);
        var pause = FindFirstObjectByType<PauseMenu>();
        pause.OpenGuide();
        yield return Wait(0.6f);
        Check(pause.GuideOpen && pause.HasSubView, "Esc → How to play opens the guide page");
        yield return Shot("t3_how_to_play");
        pause.CloseSubView();
        Check(!pause.GuideOpen, "and Esc backs out of it");
        Services.Get<GameStateService>().SetState(GameState.Playing);
        Finish();
    }

    void Finish()
    {
        Debug.Log($"[TutorTest] done, failures={failures}");
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
