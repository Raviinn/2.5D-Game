using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Beast.Core;
using Beast.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Test-only: the Settings screen and every setting's effect. Runs only with -settingsharness.</summary>
public sealed class SettingsHarness : MonoBehaviour
{
    string dir;
    int failures;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-settingsharness") < 0) return;
        var go = new GameObject("SettingsHarness");
        DontDestroyOnLoad(go);
        go.AddComponent<SettingsHarness>();
    }

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Debug.Log($"[SettingsTest] {(ok ? "PASS" : "FAIL")} {what}");
    }

    IEnumerator Start()
    {
        var args = Environment.GetCommandLineArgs();
        dir = args[Array.IndexOf(args, "-shots") + 1];
        Directory.CreateDirectory(dir);
        var state = Services.Get<GameStateService>();
        var save = Services.Get<SaveService>();
        var settings = Services.Get<SettingsService>();
        if (File.Exists(SettingsService.FilePath)) File.Delete(SettingsService.FilePath);
        settings.ResetToDefaults();

        yield return WaitFor(() => SceneManager.GetActiveScene().name == "MainMenu" && state.Current == GameState.MainMenu, 30f);
        yield return Wait(1f);

        // ---- Defaults and the runtime pipeline copy ----
        var pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        Check(pipeline != null && pipeline.name.EndsWith("(settings)"), $"graphics use a runtime copy of the URP asset ({pipeline?.name})");
        float projectShadowDistance = pipeline.shadowDistance;
        Check(QualitySettings.vSyncCount == 1 && Application.targetFrameRate == -1, "defaults: VSync on, no frame cap");
        Check(Mathf.Approximately(UITheme.Scale, Screen.height / 1080f), $"defaults: UI scale 100% ({UITheme.Scale:0.000})");

        // ---- The panel on the main menu, every tab ----
        var menu = FindFirstObjectByType<MainMenu>();
        var menuPanel = (SettingsPanel)Field(menu, "settingsPanel").GetValue(menu);
        yield return Shot("0_main_menu");
        menuPanel.Open();
        string[] tabs = { "controls", "display", "audio", "interface" };
        for (int i = 0; i < tabs.Length; i++)
        {
            SetField(menuPanel, "tab", Enum.ToObject(Field(menuPanel, "tab").FieldType, i));
            yield return Wait(0.3f);
            yield return Shot($"{i + 1}_tab_{tabs[i]}");
        }
        menuPanel.Close();

        // ---- Change everything ----
        var o = settings.Current;
        o.mouseSensitivity = 2f;
        o.invertY = true;
        o.vSync = false;
        o.frameCap = 60;
        o.renderScale = 0.5f;
        o.shadowQuality = GameSettings.ShadowsOff;
        o.antiAliasing = 4;
        o.uiScale = 1.2f;
        o.damageNumbers = false;
        o.cameraShake = 0f;
        o.masterVolume = 0.5f;
        settings.Apply();
        yield return Wait(1f);
        pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        Check(Mathf.Approximately(pipeline.renderScale, 0.5f) && pipeline.shadowDistance == 0f && pipeline.msaaSampleCount == 4,
            $"graphics applied: render scale {pipeline.renderScale}, shadow distance {pipeline.shadowDistance}, MSAA {pipeline.msaaSampleCount}");
        Check(QualitySettings.vSyncCount == 0 && Application.targetFrameRate == 60, $"VSync off, 60 fps cap ({QualitySettings.vSyncCount}, {Application.targetFrameRate})");
        Check(Mathf.Approximately(AudioListener.volume, 0.5f), $"master volume → listener ({AudioListener.volume})");
        Check(Mathf.Approximately(UITheme.Scale, Screen.height / 1080f * 1.2f), $"UI scale 120% ({UITheme.Scale:0.000})");
        string json = File.Exists(SettingsService.FilePath) ? File.ReadAllText(SettingsService.FilePath) : "";
        Check(json.Contains("\"mouseSensitivity\": 2.0") && json.Contains("\"invertY\": true"), "settings saved to settings.json");
        var reread = JsonUtility.FromJson<GameSettings>(json);
        Check(reread != null && reread.shadowQuality == 0 && Mathf.Approximately(reread.uiScale, 1.2f), "settings.json reads back");

        // ---- In the world ----
        menu.StartNewGame(0);
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "World_Test" && state.Current == GameState.Playing && GameObject.FindWithTag("Player") != null, 40f);
        yield return Wait(1.5f);
        var rig = FindFirstObjectByType<ThirdPersonCamera>();
        yield return Shot("5_world_shadows_off_scale50_ui120");

        // Mouse look: sensitivity and invert
        var mouse = InputSystem.AddDevice<Mouse>();
        float yawAt2 = YawTurn(rig, mouse, 100f, out float pitchAt2);
        yield return MoveMouse(mouse, new Vector2(100f, 50f));
        yawAt2 = Angle(rig, "yaw") - yawAt2;
        pitchAt2 = Angle(rig, "pitch") - pitchAt2;
        o.mouseSensitivity = 1f;
        o.invertY = false;
        settings.Apply();
        float yawAt1 = Angle(rig, "yaw"), pitchAt1 = Angle(rig, "pitch");
        yield return MoveMouse(mouse, new Vector2(100f, 50f));
        yawAt1 = Angle(rig, "yaw") - yawAt1;
        pitchAt1 = Angle(rig, "pitch") - pitchAt1;
        Check(yawAt1 > 1f && Mathf.Abs(yawAt2 / yawAt1 - 2f) < 0.15f, $"mouse sensitivity 2× turns twice as far ({yawAt2:0.0}° vs {yawAt1:0.0}°)");
        Check(pitchAt2 > 0f && pitchAt1 < 0f, $"invert Y flips vertical look (inverted {pitchAt2:+0.0;-0.0}°, normal {pitchAt1:+0.0;-0.0}°)");

        // Camera shake
        EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent(0.5f, 0.6f));
        Check((float)Field(rig, "shakeTimeLeft").GetValue(rig) <= 0f, "camera shake 0%: no shake");
        o.cameraShake = 1f;
        settings.Apply();
        EventBus<CameraShakeEvent>.Raise(new CameraShakeEvent(0.5f, 0.6f));
        Check((float)Field(rig, "shakeTimeLeft").GetValue(rig) > 0f, "camera shake 100%: shakes");

        // Damage numbers
        var hud = FindFirstObjectByType<CombatDebugHUD>();
        EnemyController dummy = null;
        foreach (var e in EnemyController.Active) if (e.Data.IsTrainingDummy) { dummy = e; break; }
        int before = PopupCount(hud);
        dummy.Combatant.ReceiveHit(new DamageInfo { Damage = 1f });
        yield return null;
        Check(PopupCount(hud) == before, "damage numbers off: no number");
        o.damageNumbers = true;
        settings.Apply();
        dummy.Combatant.ReceiveHit(new DamageInfo { Damage = 1f });
        yield return null;
        Check(PopupCount(hud) == before + 1, "damage numbers on: a number");

        // ---- Settings from the pause menu: Esc goes back to the pause menu first ----
        var keyboard = InputSystem.AddDevice<Keyboard>();
        state.SetState(GameState.Paused);
        var pause = FindFirstObjectByType<PauseMenu>();
        var pausePanel = (SettingsPanel)Field(pause, "settingsPanel").GetValue(pause);
        pausePanel.Open();
        SetField(pausePanel, "tab", Enum.ToObject(Field(pausePanel, "tab").FieldType, 3));
        yield return Wait(0.4f);
        yield return Shot("6_pause_settings_ui120");
        Check(state.HoldPause, "pause → Settings holds the pause");
        yield return TapKey(keyboard, Key.Escape);
        Check(state.Current == GameState.Paused, $"Esc with Settings open doesn't resume ({state.Current})");
        pausePanel.Close();
        yield return Wait(0.2f);
        Check(!state.HoldPause, "closing Settings releases the pause");
        yield return TapKey(keyboard, Key.Escape);
        Check(state.Current == GameState.Playing, $"then Esc resumes ({state.Current})");

        // ---- Reset to defaults ----
        settings.ResetToDefaults();
        yield return Wait(1f);
        pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        Check(Mathf.Approximately(pipeline.renderScale, 1f) && Mathf.Approximately(pipeline.shadowDistance, projectShadowDistance) && QualitySettings.vSyncCount == 1,
            $"reset to defaults (scale {pipeline.renderScale}, shadows {pipeline.shadowDistance} m, vsync {QualitySettings.vSyncCount})");
        Check(Mathf.Approximately(UITheme.Scale, Screen.height / 1080f), "UI scale back to 100%");
        yield return Shot("7_world_defaults");

        // ---- Display: change, confirm banner, revert by hand, then revert on its own ----
        int w = Screen.width, h = Screen.height;
        settings.SetDisplay(FullScreenMode.Windowed, 1280, 720);
        yield return Wait(1f);
        Check(Screen.width == 1280 && Screen.height == 720, $"resolution change applies ({Screen.width}×{Screen.height})");
        Check(settings.DisplayRevertSecondsLeft > 5f, $"asks to keep it ({settings.DisplayRevertSecondsLeft:0} s left)");
        state.SetState(GameState.Paused);
        pausePanel.Open();
        SetField(pausePanel, "tab", Enum.ToObject(Field(pausePanel, "tab").FieldType, 1));
        yield return Wait(0.4f);
        yield return Shot("8_display_keep_banner");
        settings.RevertDisplay();
        yield return Wait(1f);
        Check(Screen.width == w && Screen.height == h, $"Revert restores {w}×{h} ({Screen.width}×{Screen.height})");
        settings.SetDisplay(FullScreenMode.Windowed, 1280, 720);
        yield return Wait(SettingsService.DisplayConfirmSeconds + 1.5f);
        Check(Screen.width == w && Screen.height == h, $"not kept in time: reverts by itself ({Screen.width}×{Screen.height})");
        pausePanel.Close();
        state.SetState(GameState.Playing);

        Debug.Log($"[SettingsTest] done, {failures} failure(s)");
        Application.Quit();
    }

    float YawTurn(ThirdPersonCamera rig, Mouse mouse, float dx, out float pitch)
    {
        pitch = Angle(rig, "pitch");
        return Angle(rig, "yaw");
    }

    static IEnumerator MoveMouse(Mouse mouse, Vector2 delta)
    {
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = delta });
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = Vector2.zero });
        yield return null;
        yield return null;
    }

    static IEnumerator TapKey(Keyboard keyboard, Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        yield return null;
    }

    static int PopupCount(CombatDebugHUD hud)
    {
        var popups = (Array)Field(hud, "popups").GetValue(hud);
        int count = 0;
        foreach (var popup in popups)
            if (!string.IsNullOrEmpty((string)popup.GetType().GetField("Text").GetValue(popup))) count++;
        return count;
    }

    static float Angle(object target, string name) => (float)Field(target, name).GetValue(target);
    static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    static void SetField(object target, string name, object value) => Field(target, name).SetValue(target, value);
    static IEnumerator Wait(float seconds) => new WaitForSecondsRealtime(seconds);

    static IEnumerator WaitFor(Func<bool> condition, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        if (!condition()) Debug.Log("[SettingsTest] FAIL timed out waiting");
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
        Debug.Log($"[SettingsTest] shot {name}");
    }
}
