using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Test-only: the ink-and-parchment UI restyle (Docs/superpowers/plans/2026-10-07-tsushima-ui.md).
/// Step 1: fonts and brush textures exist; screenshots of the title screen (two highlights), the slot pickers,
/// the character creator and the in-world HUD in the new font. Runs only with -styleharness.
/// </summary>
public sealed class StyleHarness : MonoBehaviour
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    string dir;
    int failures;
    GameStateService state;
    SaveService save;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-styleharness") < 0) return;
        var go = new GameObject("StyleHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<StyleHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[StyleTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        state = Services.Get<GameStateService>();
        save = Services.Get<SaveService>();
        for (int i = 0; i < 3; i++)
            foreach (var path in new[] { SaveService.GetSlotPath(i), SaveService.GetSlotPath(i) + ".bak" })
                if (File.Exists(path)) File.Delete(path);

        yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, 30f);
        var menu = FindFirstObjectByType<MainMenu>();
        menu.Refresh();
        yield return Wait(2f);

        // ---- Theme ----
        Check(UITheme.FontsLoaded, "the bundled fonts load (Alegreya Sans, Cormorant Garamond)");
        var textures = UITheme.InkTextures;
        Check(textures.All(t => t != null), $"all {textures.Length} ink textures are generated");
        Check(UITheme.Body.font != null && UITheme.Body.font.name.Contains("Alegreya"), $"body text uses Alegreya Sans ({UITheme.Body.font?.name})");
        Check(UITheme.MenuItem.font != null && UITheme.MenuItem.font.name.Contains("Cormorant"), $"display text uses Cormorant Garamond ({UITheme.MenuItem.font?.name})");
        Check(UITheme.Spaced("New Game") == "N E W   G A M E", "Spaced() letter-spaces capitals");

        // ---- Title screen ----
        yield return Shot("01_title");
        SetField(menu, "highlighted", 1);
        yield return Wait(0.3f);
        yield return Shot("02_title_new_game_highlighted");
        Check((int)Field(menu, "highlighted") == 1, "the swash stays on the chosen item without the mouse");

        SetPage(menu, 1); // New Game
        yield return Wait(0.4f);
        yield return Shot("03_new_game_slots");

        menu.OpenCreator(0);
        menu.CreatorLook.hairStyle = (int)HairStyle.Ponytail;
        menu.CreatorLook.hairColor = 3;
        yield return Wait(0.5f);
        yield return Shot("04_creator");

        // ---- In the world: the HUD in the new font ----
        menu.StartNewGame(0, menu.CreatorLook);
        yield return Until(InWorld, 40f);
        Check(InWorld(), "a new game starts from the restyled creator");
        yield return Wait(3f);
        yield return Shot("05_hud_new_font");

        // ---- Step 2: the game menu, driven by real keys ----
        keyboard = InputSystem.AddDevice<Keyboard>();
        var gameMenu = FindFirstObjectByType<GameMenu>();
        var options = FindFirstObjectByType<PauseMenu>();
        Check(gameMenu != null, "the game menu is added to the world");

        yield return Tap(Key.Escape);
        Check(gameMenu.IsOpen && gameMenu.ActiveTab == GameMenuTab.Options && state.Current == GameState.Paused,
            $"Esc opens the menu on Options, paused ({gameMenu.ActiveTab}, {state.Current})");
        yield return Wait(0.3f);
        yield return Shot("08_options");
        for (int i = 0; i < 4; i++)
        {
            options.OpenCategory(i);
            yield return Wait(0.3f);
            yield return Shot($"09_options_{SettingsPanel.CategoryNames[i].ToLowerInvariant()}");
        }
        Check(options.HasSubView && state.HoldPause, "a settings category holds the pause");
        yield return Tap(Key.Escape);
        Check(gameMenu.IsOpen && state.Current == GameState.Paused && !options.HasSubView, "Esc backs out of the category, menu stays open");
        yield return Tap(Key.Escape);
        Check(!gameMenu.IsOpen && state.Current == GameState.Playing, $"Esc again closes the menu ({state.Current})");

        yield return Tap(Key.C);
        Check(gameMenu.IsOpen && gameMenu.ActiveTab == GameMenuTab.Character && state.Current == GameState.InGameMenu,
            $"C opens Character ({gameMenu.ActiveTab}, {state.Current})");
        yield return Wait(0.3f);
        yield return Shot("10_tab_character");
        yield return Tap(Key.E);
        Check(gameMenu.ActiveTab == GameMenuTab.Options && state.Current == GameState.Paused, $"E moves right to Options, paused ({gameMenu.ActiveTab}, {state.Current})");
        yield return Tap(Key.E);
        Check(gameMenu.ActiveTab == GameMenuTab.Map && state.Current == GameState.InGameMenu, $"E wraps to Map ({gameMenu.ActiveTab})");
        yield return Wait(0.4f);
        yield return Shot("11_tab_map");
        yield return Tap(Key.Q);
        Check(gameMenu.ActiveTab == GameMenuTab.Options, $"Q moves back left ({gameMenu.ActiveTab})");
        yield return Tap(Key.J);
        Check(gameMenu.ActiveTab == GameMenuTab.Journal && gameMenu.IsOpen, $"J jumps to Journal ({gameMenu.ActiveTab})");
        yield return Wait(0.3f);
        yield return Shot("12_tab_journal");
        yield return Tap(Key.J);
        Check(!gameMenu.IsOpen && state.Current == GameState.Playing, $"J again closes the menu ({state.Current})");
        yield return Tap(Key.Tab);
        Check(gameMenu.IsOpen && gameMenu.ActiveTab == GameMenuTab.Bag, $"Tab opens the Bag ({gameMenu.ActiveTab})");
        yield return Wait(0.3f);
        yield return Shot("13_tab_bag");
        yield return Tap(Key.M);
        Check(gameMenu.ActiveTab == GameMenuTab.Map, $"M jumps to Map ({gameMenu.ActiveTab})");
        yield return Tap(Key.Escape);
        Check(!gameMenu.IsOpen && state.Current == GameState.Playing && Mathf.Approximately(Time.timeScale, 1f), "Esc closes it and time runs again");
        yield return Tap(Key.M);
        Check(gameMenu.IsOpen && gameMenu.ActiveTab == GameMenuTab.Map && Mathf.Approximately(Time.timeScale, 0f), "M opens the Map tab with time frozen");
        yield return Tap(Key.Escape);

        // ---- Step 3: the tabs' own content ----
        var player = GameObject.FindWithTag("Player");
        var questLog = player.GetComponent<QuestLog>();
        var db = Services.Get<GameDatabase>();
        if (db.TryGet("quest_scraprun", out QuestData scrapRun)) questLog.StartQuest(scrapRun);
        yield return Wait(4f); // let the "quest accepted" banner pass
        gameMenu.Open(GameMenuTab.Journal);
        yield return Wait(0.4f);
        Check(questLog.ActiveQuests.Any(), "a quest to show in the Journal");
        yield return Shot("15_journal_with_quest");

        var bag = FindFirstObjectByType<InventoryScreen>();
        gameMenu.Open(GameMenuTab.Bag);
        SetField(bag, "selected", 0);
        yield return Wait(0.4f);
        yield return Shot("16_bag_selected");
        SetField(bag, "category", Enum.ToObject(typeof(InventoryScreen).GetField("category", Any).FieldType, 1)); // Food
        yield return Wait(0.3f);
        yield return Shot("17_bag_food");

        var character = FindFirstObjectByType<CharacterScreen>();
        var pageType = typeof(CharacterScreen).GetField("page", Any).FieldType;
        gameMenu.Open(GameMenuTab.Character);
        SetField(character, "page", Enum.ToObject(pageType, 0));
        yield return Wait(0.4f);
        var nodes = (System.Collections.IDictionary)Field(character, "nodeCentres");
        Check(nodes.Count >= 3, $"the Combat tree draws its skills as nodes ({nodes.Count})");
        Check(Field(character, "selectedSkill") != null, "a skill is selected for the details panel");
        yield return Shot("18_character_combat");
        SetField(character, "page", Enum.ToObject(pageType, 1));
        SetField(character, "selectedSkill", null);
        yield return Wait(0.4f);
        yield return Shot("19_character_farming");
        SetField(character, "page", Enum.ToObject(pageType, 2));
        SetField(character, "selectedSlot", 0);
        yield return Wait(0.4f);
        yield return Shot("20_character_gear");
        gameMenu.Close();
        yield return Wait(0.3f);

        // ---- Step 4: pop-ups and HUD ----
        var interactor = player.GetComponent<PlayerInteractor>();
        foreach (var shop in FindObjectsByType<Shopkeeper>(FindObjectsSortMode.None))
            if (shop.name == "Merchant") shop.Open();
        yield return Wait(0.4f);
        Check(state.Current == GameState.InGameMenu, "the shop opens");
        yield return Shot("21_shop");
        state.SetState(GameState.Playing);
        yield return Wait(0.3f);

        FindFirstObjectByType<ContractBoard>().Interact(interactor);
        yield return Wait(0.4f);
        yield return Shot("22_contract_board");
        state.SetState(GameState.Playing);
        yield return Wait(0.3f);

        DialogueSpeaker oswin = null;
        foreach (var speaker in FindObjectsByType<DialogueSpeaker>(FindObjectsSortMode.None))
            if (speaker.DisplayName == "Oswin") oswin = speaker;
        oswin.Interact(interactor);
        yield return Wait(2.5f);
        yield return Shot("23_dialogue_line");
        Services.TryGet(out DialogueRunner runner);
        for (int i = 0; i < 12 && runner.IsActive && runner.Choices.Count == 0; i++) { runner.Advance(); yield return Wait(0.05f); }
        yield return Wait(1.5f);
        Check(runner.IsActive && runner.Choices.Count > 0, "the dialogue reaches its choices");
        yield return Shot("24_dialogue_choices");
        state.SetState(GameState.Playing);
        yield return Wait(0.5f);

        if (db.TryGet("contract_supplyrun", out QuestData supply)) questLog.StartQuest(supply);
        yield return Wait(0.6f);
        yield return Shot("25_banner_and_hud");
        Services.Get<WorldClock>().AdvanceToHour(22);
        yield return Wait(4f);
        yield return Shot("26_hud_night");

        Check(save.Save(), "save, so the title offers Continue and Load Game");

        // ---- Back on the title with a save ----
        Services.Get<SceneLoader>().LoadMainMenu();
        yield return Until(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, 30f);
        yield return Wait(1.5f);
        menu = FindFirstObjectByType<MainMenu>();
        menu.Refresh();
        SetField(menu, "highlighted", 0);
        yield return Wait(0.3f);
        yield return Shot("06_title_with_continue");
        SetPage(menu, 2); // Load Game
        yield return Wait(0.4f);
        yield return Shot("07_load_game");
        SetPage(menu, 0);
        var titleSettings = (SettingsPanel)Field(menu, "settingsPanel");
        titleSettings.Open(2);
        yield return Wait(0.4f);
        yield return Shot("14_title_settings_audio");
        titleSettings.Close();

        Debug.Log($"[StyleTest] done: {failures} failure(s)");
        yield return Wait(1f);
        Application.Quit(failures == 0 ? 0 : 1);
    }

    Keyboard keyboard;

    IEnumerator Tap(Key key)
    {
        keyboard.QueueState(new KeyboardState(key));
        yield return Wait(0.08f);
        keyboard.QueueState(new KeyboardState());
        yield return null;
        yield return null;
    }

    static void SetPage(MainMenu menu, int page)
    {
        var method = typeof(MainMenu).GetMethod("Open", Any);
        method.Invoke(menu, new[] { Enum.ToObject(typeof(MainMenu).GetField("page", Any).FieldType, page) });
    }

    static bool InWorld() =>
        SceneManager.GetActiveScene().name == "World_Test" && GameObject.FindWithTag("Player") != null &&
        Services.Get<GameStateService>().Current == GameState.Playing;

    static object Field(object target, string name) => target.GetType().GetField(name, Any).GetValue(target);

    static void SetField(object target, string name, object value) => target.GetType().GetField(name, Any).SetValue(target, value);

    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

    static IEnumerator Until(Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log($"[StyleTest] shot {name}");
    }
}
