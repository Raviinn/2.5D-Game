using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Test-only: drives the game through every UI screen and captures screenshots. Runs only with -uiharness.</summary>
public sealed class UIHarness : MonoBehaviour
{
    string dir;
    GameStateService state;
    GameObject player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-uiharness") < 0) return;
        var go = new GameObject("UIHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<UIHarness>();
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return new WaitForSecondsRealtime(3f);
        Debug.Log("[Harness] world loaded");

        state = Services.Get<GameStateService>();
        player = GameObject.FindWithTag("Player");
        var inventory = player.GetComponent<Inventory>();
        var log = player.GetComponent<QuestLog>();
        var db = Services.Get<GameDatabase>();
        var combatant = player.GetComponent<Combatant>();

        yield return Shot("01_hud_start");

        // Notifications, a quest, XP: HUD feed, tracker, waypoint and banner.
        db.TryGet("quest_bandittrouble", out QuestData bandits);
        db.TryGet("quest_tasteofhome", out QuestData taste);
        log.StartQuest(bandits);
        log.StartQuest(taste);
        if (db.TryGet("item_turnip", out ItemData turnip)) inventory.Add(turnip, 3);
        inventory.AddGold(25);
        player.GetComponent<PlayerProgression>().AddXp(Discipline.Combat, 40);
        yield return Wait(0.6f);
        yield return Shot("02_hud_quest_banner");

        // Low health pulse.
        combatant.ReceiveHit(new DamageInfo { Damage = combatant.MaxHealth * 0.8f });
        yield return Wait(1.2f);
        yield return Shot("03_hud_low_health");
        combatant.Heal(9999f);

        yield return OpenTab<InventoryScreen>(GameMenuTab.Bag, "04_inventory", s => SetField(s, "selected", 0));
        yield return OpenTab<CharacterScreen>(GameMenuTab.Character, "05_character", null);
        yield return OpenTab<QuestJournal>(GameMenuTab.Journal, "06_journal", s => SetField(s, "selected", log.FocusedQuest));

        var interactor = player.GetComponent<PlayerInteractor>();
        var board = FindFirstObjectByType<ContractBoard>();
        board.Interact(interactor);
        yield return Wait(0.3f);
        yield return Shot("07_contract_board");
        state.SetState(GameState.Playing);

        foreach (var shop in FindObjectsByType<Shopkeeper>(FindObjectsSortMode.None))
            if (shop.name == "Merchant") shop.Open();
        yield return Wait(0.3f);
        yield return Shot("08_shop");
        state.SetState(GameState.Playing);

        state.SetState(GameState.Paused); // the game menu opens on Options
        yield return null;
        FindFirstObjectByType<PauseMenu>().OpenCategory(0); // Controls: the key list
        yield return Wait(0.3f);
        yield return Shot("09_pause_controls");
        state.SetState(GameState.Playing);

        // Dialogue: Oswin's intro, then advance to the first choices.
        DialogueSpeaker oswin = null;
        foreach (var speaker in FindObjectsByType<DialogueSpeaker>(FindObjectsSortMode.None))
            if (speaker.DisplayName == "Oswin") oswin = speaker;
        oswin.Interact(interactor);
        yield return Wait(2.5f);
        yield return Shot("10_dialogue_line");
        Services.TryGet(out DialogueRunner runner);
        for (int i = 0; i < 12 && runner.IsActive && runner.Choices.Count == 0; i++) { runner.Advance(); yield return Wait(0.05f); }
        yield return Wait(1.5f);
        yield return Shot("11_dialogue_choices");
        state.SetState(GameState.Playing);
        yield return Wait(0.3f);

        // Large map.
        var gameMenu = FindFirstObjectByType<GameMenu>();
        gameMenu.Open(GameMenuTab.Map);
        yield return Wait(0.4f);
        yield return Shot("12_large_map");
        gameMenu.Close();

        // Interaction prompt at the bed, then sleep (fade).
        var bed = FindFirstObjectByType<SleepSpot>();
        var motor = player.GetComponent<PlayerMotor>();
        var toBed = bed.transform.position - new Vector3(1.4f, 0f, 0f);
        motor.Teleport(new Vector3(toBed.x, player.transform.position.y, toBed.z), Quaternion.LookRotation(Vector3.right));
        yield return Wait(0.8f);
        yield return Shot("13_prompt_bed");
        bed.Interact(interactor);
        yield return Wait(0.5f);
        yield return Shot("14_sleep_fade");
        yield return Wait(2.2f);

        // Defeat overlay.
        combatant.ReceiveHit(new DamageInfo { Damage = 99999f });
        yield return Wait(0.5f);
        yield return Shot("15_defeated");
        yield return Wait(2.5f);
        yield return Shot("16_after_respawn");

        Debug.Log("[Harness] done");
        Application.Quit();
    }

    IEnumerator OpenTab<T>(GameMenuTab tab, string shot, Action<T> configure) where T : MonoBehaviour
    {
        var menu = FindFirstObjectByType<GameMenu>();
        menu.Open(tab);
        configure?.Invoke(FindFirstObjectByType<T>());
        yield return Wait(0.3f);
        yield return Shot(shot);
        menu.Close();
        yield return Wait(0.2f);
    }

    static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log($"[Harness] shot {name}");
    }
}
