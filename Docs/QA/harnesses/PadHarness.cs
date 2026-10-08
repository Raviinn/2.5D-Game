using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>Test-only: controller navigation through the menus with a virtual gamepad (Milestone 35). Runs only with -padharness.</summary>
public sealed class PadHarness : MonoBehaviour
{
    string dir;
    int failures;
    Gamepad pad;
    Mouse mouse;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-padharness") < 0) return;
        var go = new GameObject("PadHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<PadHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[PadTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    /// <summary>Holds pad buttons / D-pad for two frames, then lets go.</summary>
    IEnumerator Press(GamepadButton button)
    {
        var state = new GamepadState().WithButton(button);
        InputSystem.QueueStateEvent(pad, state);
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(pad, new GamepadState());
        yield return null;
        yield return null;
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        for (int i = 0; i < SaveService.SlotCount; i++)
            foreach (var path in new[] { SaveService.GetSlotPath(i), SaveService.GetSlotPath(i) + ".bak" })
                if (File.Exists(path)) File.Delete(path);

        MainMenu menu = null;
        float end = Time.realtimeSinceStartup + 40f;
        while ((menu = FindFirstObjectByType<MainMenu>()) == null && Time.realtimeSinceStartup < end) yield return null;
        yield return Wait(2f);
        // The PC's own mouse and keyboard still reach this window in the background (HarnessFocus): someone using the
        // computer would flip the menus to mouse mode mid-test. Switch them off and use a virtual mouse instead.
        foreach (var device in InputSystem.devices.ToArray())
            if (device is Mouse or Keyboard) InputSystem.DisableDevice(device);
        pad = InputSystem.AddDevice<Gamepad>("HarnessPad");
        mouse = InputSystem.AddDevice<Mouse>("HarnessMouse");
        mouse.MakeCurrent();
        pad.MakeCurrent();
        var state = Services.Get<GameStateService>();
        Check(menu != null, "title screen");
        if (menu == null) { Finish(); yield break; }

        // ---- Title: the first press shows the focus; the next ones move it; A chooses; B goes back ----
        yield return Press(GamepadButton.DpadDown);
        Check(UITheme.NavShowing && UITheme.UsingGamepad, $"the first D-pad press shows the focus ({UITheme.NavControlCount} controls on screen)");
        int before = (int)Field(menu, "highlighted");
        yield return Press(GamepadButton.DpadDown);
        int after = (int)Field(menu, "highlighted");
        Check(after != before, $"D-pad down moves the swash ({before} -> {after})");
        // New Game is the first item on a fresh install: go back up to it.
        for (int i = 0; i < 6 && (int)Field(menu, "highlighted") != 0; i++) yield return Press(GamepadButton.DpadUp);
        yield return Shot("p1_title");
        Debug.Log($"[PadTest] INFO before A: showing {UITheme.NavShowing}, pad {UITheme.UsingGamepad}, focus {UITheme.NavFocus}, highlighted {Field(menu, "highlighted")}, controls {UITheme.NavControlCount}");
        yield return Press(GamepadButton.South);
        Debug.Log($"[PadTest] INFO after A: showing {UITheme.NavShowing}, focus {UITheme.NavFocus}, page {Field(menu, "page")}");
        yield return Wait(0.3f);
        Check(Field(menu, "page").ToString() == "NewGame", $"A on New Game opens the slots ({Field(menu, "page")})");
        yield return Shot("p2_slots");
        yield return Press(GamepadButton.East);
        yield return Wait(0.3f);
        Check(Field(menu, "page").ToString() == "Home", $"B goes back ({Field(menu, "page")})");

        // ---- The creator ----
        menu.OpenCreator(0);
        yield return Wait(0.4f);
        string name = menu.CreatorLook.name;
        // With the focus hidden, the first press shows it on the top-left control: the Random name button.
        UITheme.HideNavFocus();
        yield return Press(GamepadButton.DpadUp);
        Debug.Log($"[PadTest] INFO creator focus {UITheme.NavFocus}");
        yield return Press(GamepadButton.South);
        Check(!string.IsNullOrEmpty(menu.CreatorLook.name) && menu.CreatorLook.name != name, $"Random name works from the pad ({menu.CreatorLook.name})");
        int hair = menu.CreatorLook.hairStyle;
        yield return Press(GamepadButton.DpadDown); // the Hair stepper
        Debug.Log($"[PadTest] INFO hair focus {UITheme.NavFocus} adjustable {UITheme.NavFocusAdjustable} showing {UITheme.NavShowing}");
        yield return Press(GamepadButton.DpadRight);
        Debug.Log($"[PadTest] INFO after right {UITheme.NavFocus} hair {menu.CreatorLook.hairStyle}");
        Check(menu.CreatorLook.hairStyle == hair + 1, $"right on the Hair stepper changes the hair ({hair} -> {menu.CreatorLook.hairStyle})");
        yield return Shot("p3_creator");
        menu.StartNewGame(0, menu.CreatorLook);
        end = Time.realtimeSinceStartup + 40f;
        while (SceneManager.GetActiveScene().name != "World_Test" && Time.realtimeSinceStartup < end) yield return null;
        yield return Wait(3f);


        // ---- Game menu: View opens the bag; the D-pad moves between slots; LB / RB switch tabs; B closes ----
        yield return Press(GamepadButton.Select);
        yield return Wait(0.3f);
        var gameMenu = FindFirstObjectByType<GameMenu>();
        Check(gameMenu != null && gameMenu.IsOpen && state.Current == GameState.InGameMenu, "View opens the game menu");
        Check(UITheme.KeyLabel("F") == "↑" && UITheme.KeyLabel("Esc") == "B", "key hints switch to pad buttons");
        yield return Press(GamepadButton.DpadRight);
        yield return Press(GamepadButton.DpadRight);
        Check(UITheme.NavShowing && UITheme.NavControlCount > 5, $"menu controls are navigable ({UITheme.NavControlCount})");
        yield return Shot("p5_bag_focus");
        var tab = gameMenu.ActiveTab;
        yield return Press(GamepadButton.RightShoulder);
        Check(gameMenu.ActiveTab != tab, $"RB switches tab ({tab} -> {gameMenu.ActiveTab})");
        yield return Press(GamepadButton.East);
        yield return Wait(0.3f);
        Check(state.Current == GameState.Playing, $"B closes the menu ({state.Current})");

        // ---- Shop: buy with the pad ----
        var shop = FindFirstObjectByType<Shopkeeper>();
        var inventory = GameObject.FindWithTag("Player").GetComponent<Inventory>();
        inventory.AddGold(500);
        int gold = inventory.Gold;
        UITheme.HideNavFocus(); // the first press starts at the top-left control
        shop.Open();
        yield return Wait(0.3f);
        yield return Press(GamepadButton.DpadDown); // shows the focus on the first control (the first Buy button)
        yield return Press(GamepadButton.South);
        yield return Wait(0.2f);
        Check(inventory.Gold < gold, $"A on a Buy button buys ({gold} -> {inventory.Gold} gold)");
        yield return Shot("p6_shop");
        yield return Press(GamepadButton.East);
        yield return Wait(0.3f);
        Check(state.Current == GameState.Playing, "B leaves the shop");

        // ---- Options: a slider moves with left / right ----
        yield return Press(GamepadButton.Start);
        yield return Wait(0.3f);
        Check(state.Current == GameState.Paused, $"Start opens Options ({state.Current})");
        var pause = FindFirstObjectByType<PauseMenu>();
        pause.OpenCategory(2); // Audio
        yield return Wait(0.3f);
        var settings = Services.Get<SettingsService>();
        float master = settings.Current.masterVolume;
        // Down until a slider has the focus (the first adjustable row in Audio is Master volume), then left.
        for (int i = 0; i < 12 && !UITheme.NavFocusAdjustable; i++)
        {
            yield return Press(GamepadButton.DpadDown);
            Debug.Log($"[PadTest] INFO options focus {UITheme.NavFocus} adjustable {UITheme.NavFocusAdjustable} showing {UITheme.NavShowing} controls {UITheme.NavControlCount}");
        }
        yield return Press(GamepadButton.DpadLeft);
        Check(!Mathf.Approximately(settings.Current.masterVolume, master), $"left on the Master volume slider lowers it ({master:0.00} -> {settings.Current.masterVolume:0.00})");
        yield return Shot("p7_options_slider");
        settings.Current.masterVolume = master;
        settings.Apply();
        yield return Press(GamepadButton.East);
        yield return Wait(0.3f);

        // ---- The mouse hides the focus ----
        state.SetState(GameState.Paused);
        yield return Wait(0.8f); // past the moment the cursor jumps as it unlocks
        yield return Press(GamepadButton.DpadDown);
        bool shownBefore = UITheme.NavShowing;
        yield return WiggleMouse();
        Check(shownBefore && !UITheme.NavShowing, "moving the mouse hides the focus frame");
        state.SetState(GameState.Playing);

        Finish();
    }

    IEnumerator WiggleMouse()
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(300f, 300f) });
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(420f, 360f) });
        yield return null;
        yield return null;
    }

    static object Field(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);

    void Finish()
    {
        Debug.Log($"[PadTest] done, failures={failures}");
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
