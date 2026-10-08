using System;
using System.Collections;
using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>Test-only: key rebinding (Milestone 50). Runs with -keysharness.</summary>
public sealed class KeysHarness : MonoBehaviour
{
    string dir;
    int failures;
    Keyboard keyboard;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-keysharness") < 0) return;
        var go = new GameObject("KeysHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<KeysHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[KeysTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        while (SceneManager.GetActiveScene().name != "World_Test" || GameObject.FindWithTag("Player") == null) yield return null;
        yield return Wait(3f);

        var input = Services.Get<InputService>();
        var settings = Services.Get<SettingsService>();
        // The real keyboard stays, but the test presses keys on its own.
        keyboard = InputSystem.AddDevice<Keyboard>("HarnessKeyboard");
        int interactRow = Array.FindIndex(KeyBindings.Rows, r => r.Action == "Interact");
        int sprintRow = Array.FindIndex(KeyBindings.Rows, r => r.Action == "Sprint");
        int skill1Row = Array.FindIndex(KeyBindings.Rows, r => r.Action == "Skill1");
        int forwardRow = Array.FindIndex(KeyBindings.Rows, r => r.Part == "up");

        Check(KeyBindings.Rows.Length >= 20, $"{KeyBindings.Rows.Length} actions can be rebound");
        Check(KeyBindings.Label(KeyBindings.Rows[interactRow], false) == "F" && KeyBindings.Label(KeyBindings.Rows[interactRow], true) == "↑",
            "Interact reads F / D-pad up by default");
        Check(KeyBindings.Label(KeyBindings.Rows[forwardRow], false) == "W", "Move forward reads W");
        Check(UITheme.KeyLabel("F") == "F", "the HUD shows [F] before rebinding");

        // ---- Bind Interact to G ----
        KeyBindings.Bind(interactRow, false, "<Keyboard>/g");
        Save(settings);
        Check(KeyBindings.Label(KeyBindings.Rows[interactRow], false) == "G", "Interact rebound to G");
        Check(UITheme.KeyLabel("F") == "G", $"the HUD's interact key cap now reads [{UITheme.KeyLabel("F")}]");
        yield return Press(Key.G);
        bool gWorks = input.Interact.IsPressed();
        yield return Release();
        yield return Press(Key.F);
        bool fIdle = !input.Interact.IsPressed();
        yield return Release();
        Check(gWorks, "pressing G interacts");
        Check(fIdle, "F no longer does");

        // ---- Sprint and dodge move together ----
        KeyBindings.Bind(sprintRow, false, "<Keyboard>/leftCtrl");
        var dodge = input.Actions.FindAction("Dodge");
        int dodgeIndex = KeyBindings.BindingIndex(dodge, KeyBindings.Rows[sprintRow], false);
        Check(dodge.bindings[dodgeIndex].effectivePath == "<Keyboard>/leftCtrl", "rebinding sprint also moves dodge (they share a key)");

        // ---- Clashes are visible ----
        KeyBindings.Bind(skill1Row, false, "<Keyboard>/q");
        int skill2Row = Array.FindIndex(KeyBindings.Rows, r => r.Action == "Skill2");
        Check(KeyBindings.EffectivePath(KeyBindings.Rows[skill1Row], false) == KeyBindings.EffectivePath(KeyBindings.Rows[skill2Row], false),
            "two actions on Q can be detected (shown as a clash)");
        KeyBindings.Bind(skill1Row, false, "<Keyboard>/e");

        // ---- Interactive rebind: press a key, or Esc to cancel ----
        bool? result = null;
        KeyBindings.StartRebind(interactRow, false, bound => result = bound);
        Check(KeyBindings.IsRebinding, "waiting for a key");
        yield return Wait(0.3f);
        yield return Press(Key.H);
        yield return Release();
        yield return WaitFor(() => result.HasValue, 2f);
        Check(result == true && KeyBindings.Label(KeyBindings.Rows[interactRow], false) == "H", $"pressing H binds it ({KeyBindings.Label(KeyBindings.Rows[interactRow], false)})");
        result = null;
        KeyBindings.StartRebind(interactRow, false, bound => result = bound);
        yield return Wait(0.3f);
        var state = Services.Get<GameStateService>();
        state.SetState(GameState.InGameMenu);
        yield return Wait(0.2f);
        yield return Press(Key.Escape);
        yield return Release();
        yield return WaitFor(() => result.HasValue, 2f);
        Check(result == false && KeyBindings.Label(KeyBindings.Rows[interactRow], false) == "H", "Esc cancels and keeps H");
        yield return Wait(0.2f);
        Check(state.Current == GameState.InGameMenu, "and doesn't close the menu it was pressed in");
        state.SetState(GameState.Playing);
        Save(settings);

        // ---- Saved per PC ----
        string json = settings.Current.bindingOverrides;
        Check(!string.IsNullOrEmpty(json) && json.Contains("<Keyboard>/h"), "the bindings are saved in the settings");
        input.ApplyBindingOverrides(string.Empty);
        Check(KeyBindings.Label(KeyBindings.Rows[interactRow], false) == "F", "(cleared for the test)");
        settings.Apply();
        Check(KeyBindings.Label(KeyBindings.Rows[interactRow], false) == "H", "and come back from the settings");

        // ---- The settings screen ----
        var menu = FindFirstObjectByType<GameMenu>();
        menu.Open(GameMenuTab.Options);
        yield return Wait(0.3f);
        FindFirstObjectByType<PauseMenu>().OpenCategory(0);
        yield return Wait(0.6f);
        yield return Shot("k1_controls");
        state.SetState(GameState.Playing);

        // ---- Reset ----
        KeyBindings.ResetAll();
        settings.Current.bindingOverrides = string.Empty;
        settings.Apply();
        Check(KeyBindings.Label(KeyBindings.Rows[interactRow], false) == "F" && UITheme.KeyLabel("F") == "F", "Reset keys puts everything back");
        settings.Save();
        InputSystem.RemoveDevice(keyboard);
        Finish();
    }

    static void Save(SettingsService settings)
    {
        settings.Current.bindingOverrides = KeyBindings.SaveOverrides();
        settings.Apply();
    }

    IEnumerator Press(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        yield return null;
    }

    IEnumerator Release()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        yield return null;
    }

    static IEnumerator WaitFor(Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
    }

    void Finish()
    {
        Debug.Log($"[KeysTest] done, failures={failures}");
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
