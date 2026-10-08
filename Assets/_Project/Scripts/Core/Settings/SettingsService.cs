using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Beast.Core
{
    /// <summary>
    /// The player's options. Shared by every save slot (settings.json next to the saves), not part of a save.
    /// Display mode and resolution aren't stored here: Unity remembers those itself.
    /// </summary>
    [Serializable]
    public sealed class GameSettings
    {
        public const int ShadowsOff = 0, ShadowsLow = 1, ShadowsMedium = 2, ShadowsHigh = 3;

        [Header("Controls")]
        [Tooltip("Multiplier on the camera's mouse look speed.")] public float mouseSensitivity = 1f;
        [Tooltip("Multiplier on the camera's controller stick speed.")] public float stickSensitivity = 1f;
        public bool invertY;

        [Header("Display & graphics")]
        public bool vSync = true;
        [Tooltip("0 = no limit. Ignored while VSync is on.")] public int frameCap;
        [Tooltip("Resolution the 3D world renders at (UI stays sharp). Lower = faster.")] public float renderScale = 1f;
        public int shadowQuality = ShadowsHigh;
        [Tooltip("MSAA samples: 1 (off), 2, 4 or 8.")] public int antiAliasing = 1;

        [Header("Audio")]
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float effectsVolume = 1f;
        [Tooltip("Wind, birds, crickets, rain.")] public float ambienceVolume = 0.8f;
        [Tooltip("Menu clicks and quest / level-up chimes.")] public float interfaceVolume = 0.8f;

        [Header("Key bindings (Milestone 50)")]
        [Tooltip("Rebound keys and buttons, as Input System override JSON. Empty = the defaults.")]
        public string bindingOverrides = string.Empty;

        [Header("Interface")]
        public float uiScale = 1f;
        public bool damageNumbers = true;
        [Tooltip("0 = no camera shake, 1 = full.")] public float cameraShake = 1f;
        [Tooltip("The first-day guide and one-off tips (Milestone 53).")] public bool tutorialHints = true;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        /// <summary>Pulls every value back into its valid range (hand-edited or old files).</summary>
        public void Clamp()
        {
            mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.25f, 3f);
            stickSensitivity = Mathf.Clamp(stickSensitivity, 0.25f, 3f);
            frameCap = frameCap <= 0 ? 0 : Mathf.Clamp(frameCap, 30, 360);
            renderScale = Mathf.Clamp(renderScale, 0.5f, 1f);
            shadowQuality = Mathf.Clamp(shadowQuality, ShadowsOff, ShadowsHigh);
            antiAliasing = antiAliasing >= 8 ? 8 : antiAliasing >= 4 ? 4 : antiAliasing >= 2 ? 2 : 1;
            masterVolume = Mathf.Clamp01(masterVolume);
            musicVolume = Mathf.Clamp01(musicVolume);
            effectsVolume = Mathf.Clamp01(effectsVolume);
            ambienceVolume = Mathf.Clamp01(ambienceVolume);
            interfaceVolume = Mathf.Clamp01(interfaceVolume);
            uiScale = Mathf.Clamp(uiScale, 0.8f, 1.2f);
            cameraShake = Mathf.Clamp01(cameraShake);
        }
    }

    /// <summary>Raised after any setting changes (and once at boot).</summary>
    public readonly struct SettingsChangedEvent : IEvent
    {
        public readonly GameSettings Settings;
        public SettingsChangedEvent(GameSettings settings) { Settings = settings; }
    }

    /// <summary>
    /// Loads, applies and saves the player's settings. Change values on Current, then call Apply(): it takes
    /// effect at once and is written to disk half a second after the last change (so dragging a slider doesn't
    /// write every frame). Graphics options edit a runtime copy of the render pipeline asset, never the project's.
    /// Window mode / resolution changes ask to be kept and revert by themselves after a few seconds.
    /// </summary>
    public sealed class SettingsService : MonoBehaviour
    {
        const string FileName = "settings.json";
        const float SaveDelay = 0.5f;
        public const float DisplayConfirmSeconds = 12f;

        public GameSettings Current { get; private set; } = new();

        UniversalRenderPipelineAsset projectPipeline;
        UniversalRenderPipelineAsset runtimePipeline;
        RenderPipelineAsset originalQualityPipeline;
        bool pipelineSwapped;
        float saveAt = -1f;

        // Display change waiting for "Keep"
        FullScreenMode previousMode;
        int previousWidth, previousHeight;
        float revertAt = -1f;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Seconds left to keep a display change before it reverts, or 0 when nothing is pending.</summary>
        public float DisplayRevertSecondsLeft => revertAt < 0f ? 0f : Mathf.Max(0f, revertAt - Time.unscaledTime);

        /// <summary>
        /// A category's volume WITHOUT the master volume: master is applied once, through AudioListener.volume.
        /// </summary>
        public float MusicVolume => Current.musicVolume;
        public float EffectsVolume => Current.effectsVolume;
        public float AmbienceVolume => Current.ambienceVolume;
        public float InterfaceVolume => Current.interfaceVolume;

        public void Initialize()
        {
            Current = Load();
            Application.quitting += RestorePipeline;
            Apply();
        }

        void OnDestroy()
        {
            Application.quitting -= RestorePipeline;
            RestorePipeline();
        }

        void Update()
        {
            if (saveAt >= 0f && Time.unscaledTime >= saveAt) Save();
            if (revertAt >= 0f && Time.unscaledTime >= revertAt) RevertDisplay();
        }

        // ---------- Apply ----------

        /// <summary>Applies everything in Current (except display mode / resolution) and saves soon.</summary>
        public void Apply()
        {
            Current.Clamp();

            QualitySettings.vSyncCount = Current.vSync ? 1 : 0;
            Application.targetFrameRate = !Current.vSync && Current.frameCap > 0 ? Current.frameCap : -1;
            ApplyGraphics();
            AudioListener.volume = Current.masterVolume;
            if (Services.TryGet(out InputService input)) input.ApplyBindingOverrides(Current.bindingOverrides);

            saveAt = Time.unscaledTime + SaveDelay;
            EventBus<SettingsChangedEvent>.Raise(new SettingsChangedEvent(Current));
        }

        /// <summary>Puts every option back to its default (display mode and resolution are left alone).</summary>
        public void ResetToDefaults()
        {
            Current = new GameSettings();
            Apply();
        }

        void ApplyGraphics()
        {
            if (runtimePipeline == null && !CreateRuntimePipeline()) return;

            runtimePipeline.renderScale = Current.renderScale;
            runtimePipeline.msaaSampleCount = Current.antiAliasing;
            switch (Current.shadowQuality)
            {
                case GameSettings.ShadowsOff:
                    runtimePipeline.shadowDistance = 0f; // URP renders no main light shadows at distance 0
                    break;
                case GameSettings.ShadowsLow:
                    runtimePipeline.shadowDistance = 30f;
                    runtimePipeline.mainLightShadowmapResolution = 1024;
                    runtimePipeline.shadowCascadeCount = 1;
                    break;
                case GameSettings.ShadowsMedium:
                    runtimePipeline.shadowDistance = 40f;
                    runtimePipeline.mainLightShadowmapResolution = 2048;
                    runtimePipeline.shadowCascadeCount = 2;
                    break;
                default: // High = the project's own settings
                    runtimePipeline.shadowDistance = projectPipeline.shadowDistance;
                    runtimePipeline.mainLightShadowmapResolution = projectPipeline.mainLightShadowmapResolution;
                    runtimePipeline.shadowCascadeCount = projectPipeline.shadowCascadeCount;
                    break;
            }
        }

        /// <summary>
        /// Graphics options edit a copy of the active URP asset, so playing in the Editor never changes the
        /// project's asset. The original is put back when the game quits (or Play mode stops).
        /// </summary>
        bool CreateRuntimePipeline()
        {
            originalQualityPipeline = QualitySettings.renderPipeline;
            projectPipeline = (originalQualityPipeline != null ? originalQualityPipeline : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (projectPipeline == null) return false;

            runtimePipeline = Instantiate(projectPipeline);
            runtimePipeline.name = projectPipeline.name + " (settings)";
            runtimePipeline.hideFlags = HideFlags.DontSave;
            QualitySettings.renderPipeline = runtimePipeline;
            pipelineSwapped = true;
            return true;
        }

        void RestorePipeline()
        {
            if (!pipelineSwapped) return;
            pipelineSwapped = false;
            QualitySettings.renderPipeline = originalQualityPipeline;
            if (runtimePipeline != null) Destroy(runtimePipeline);
            runtimePipeline = null;
        }

        // ---------- Display ----------

        /// <summary>Changes window mode / resolution now; it reverts by itself unless KeepDisplay() is called in time.</summary>
        public void SetDisplay(FullScreenMode mode, int width, int height)
        {
            if (mode == Screen.fullScreenMode && width == Screen.width && height == Screen.height) return;
            if (revertAt < 0f)
            {
                previousMode = Screen.fullScreenMode;
                previousWidth = Screen.width;
                previousHeight = Screen.height;
            }
            Screen.SetResolution(width, height, mode);
            // In the Editor the Game view ignores this, so there's nothing to confirm.
            revertAt = Application.isEditor ? -1f : Time.unscaledTime + DisplayConfirmSeconds;
            Debug.Log($"[Settings] Display → {mode} {width}×{height}");
        }

        public void KeepDisplay() => revertAt = -1f;

        public void RevertDisplay()
        {
            if (revertAt < 0f) return;
            revertAt = -1f;
            Screen.SetResolution(previousWidth, previousHeight, previousMode);
            Debug.Log($"[Settings] Display reverted → {previousMode} {previousWidth}×{previousHeight}");
        }

        /// <summary>Distinct resolutions the monitor supports (at least 1280×720), smallest first.</summary>
        public static List<Vector2Int> AvailableResolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (size.x >= 1280 && size.y >= 720 && !list.Contains(size)) list.Add(size);
            }
            var current = new Vector2Int(Screen.width, Screen.height);
            if (!list.Contains(current)) list.Add(current);
            list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            return list;
        }

        // ---------- File ----------

        static GameSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonUtility.FromJson<GameSettings>(File.ReadAllText(FilePath));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] Couldn't read {FilePath} ({e.Message}); using defaults.");
            }
            return new GameSettings();
        }

        /// <summary>Writes the settings now (normally done automatically shortly after a change).</summary>
        public void Save()
        {
            saveAt = -1f;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void OnApplicationQuit()
        {
            if (saveAt >= 0f) Save();
        }
    }
}
