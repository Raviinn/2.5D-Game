using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Settings on parchment (UI restyle step 2), in four categories: Controls · Display · Audio · Interface.
    /// - The title screen draws it as a window (Draw): category cards along the top, the rows, Reset and Back.
    /// - The Options tab of the game menu draws one category inline (DrawCategory) when its tile is chosen.
    /// Every change applies at once and saves itself shortly after (SettingsService); window mode / resolution changes
    /// ask to be kept and revert after a few seconds.
    /// </summary>
    public sealed class SettingsPanel
    {
        enum Tab { Controls, Display, Audio, Interface }

        public static readonly string[] CategoryNames = { "Controls", "Display", "Audio", "Interface" };
        static readonly string[] WindowModeNames = { "Borderless", "Fullscreen", "Windowed" };
        static readonly FullScreenMode[] WindowModes = { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
        static readonly string[] FrameCapNames = { "30", "60", "120", "144", "No limit" };
        static readonly int[] FrameCaps = { 30, 60, 120, 144, 0 };
        static readonly string[] ShadowNames = { "Off", "Low", "Medium", "High" };
        static readonly string[] AntiAliasingNames = { "Off", "2×", "4×", "8×" };
        static readonly int[] AntiAliasingSamples = { 1, 2, 4, 8 };

        const float Width = 920f;
        const float Height = 720f;
        const float RowHeight = 54f;
        const float LabelWidth = 290f;

        Tab tab;
        bool confirmReset;
        List<Vector2Int> resolutions = new();
        static GUIStyle rowLabel, valueLabel, bannerText;

        public bool IsOpen { get; private set; }

        /// <summary>The category being shown (0 Controls, 1 Display, 2 Audio, 3 Interface).</summary>
        public int Category
        {
            get => (int)tab;
            set => tab = (Tab)Mathf.Clamp(value, 0, CategoryNames.Length - 1);
        }

        public void Open() => Open(Category);

        public void Open(int category)
        {
            IsOpen = true;
            Category = category;
            confirmReset = false;
            resolutions = SettingsService.AvailableResolutions();
        }

        public void Close()
        {
            IsOpen = false;
            confirmReset = false;
            if (Services.TryGet(out SettingsService settings))
            {
                settings.KeepDisplay(); // leaving the screen counts as keeping what you see
                settings.Save();
            }
        }

        /// <summary>The title screen's Settings window (call inside a UITheme.Begin OnGUI). Esc or Back closes it.</summary>
        public void Draw()
        {
            if (!IsOpen) return;
            if (!Services.TryGet(out SettingsService _))
            {
                IsOpen = false;
                return;
            }
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Event.current.Use();
                Close();
                return;
            }

            var content = UITheme.ParchmentWindow(Width, Mathf.Min(Height, UITheme.Height - 32f), "Settings",
                "Changes apply right away and are saved for every slot", backdrop: false);
            Category = UITheme.PaperOptions(new Rect(content.x, content.y, content.width, 46f), Category, CategoryNames);
            DrawCategory(new Rect(content.x, content.y + 62f, content.width, content.height - 62f - 64f), Category);

            float y = content.yMax - 48f;
            DrawReset(new Rect(content.x, y, 320f, 48f));
            if (UITheme.BrushButton(new Rect(content.xMax - 180f, y, 180f, 48f), "Back")) Close();
        }

        /// <summary>One category's rows on parchment (also the Options tab's inline view).</summary>
        public void DrawCategory(Rect area, int category)
        {
            if (!Services.TryGet(out SettingsService settings)) return;
            if (resolutions.Count == 0) resolutions = SettingsService.AvailableResolutions();
            EnsureStyles();
            var options = settings.Current;
            GUI.changed = false;
            float y = area.y + 6f;
            switch ((Tab)category)
            {
                case Tab.Controls: DrawControls(area, ref y, options); break;
                case Tab.Display: DrawDisplay(area, ref y, options, settings); break;
                case Tab.Audio: DrawAudio(area, ref y, options); break;
                case Tab.Interface: DrawInterface(area, ref y, options); break;
            }
            if (GUI.changed) settings.Apply();
        }

        /// <summary>"Reset to defaults" with a confirm (every setting but window mode and resolution).</summary>
        public void DrawReset(Rect rect)
        {
            if (!Services.TryGet(out SettingsService settings)) return;
            EnsureStyles();
            if (!confirmReset)
            {
                if (UITheme.BrushButton(rect, "Reset to defaults")) confirmReset = true;
                return;
            }
            GUI.Label(new Rect(rect.x, rect.y - 28f, 520f, 24f),
                $"<color={UITheme.VermilionHex}>Reset every setting (not window mode or resolution)?</color>", rowLabel);
            float half = rect.width * 0.5f - 5f;
            if (UITheme.BrushButton(new Rect(rect.x, rect.y, half, rect.height), "Reset"))
            {
                settings.ResetToDefaults();
                confirmReset = false;
            }
            if (UITheme.BrushButton(new Rect(rect.x + half + 10f, rect.y, half, rect.height), "Cancel")) confirmReset = false;
        }

        // ---------- Categories ----------

        void DrawControls(Rect area, ref float y, GameSettings o)
        {
            o.mouseSensitivity = SliderRow(area, ref y, "Mouse sensitivity", o.mouseSensitivity, 0.25f, 3f, 0.05f, $"{o.mouseSensitivity:0.00}×");
            o.stickSensitivity = SliderRow(area, ref y, "Controller look speed", o.stickSensitivity, 0.25f, 3f, 0.05f, $"{o.stickSensitivity:0.00}×");
            o.invertY = ToggleRow(area, ref y, "Invert vertical look", o.invertY);
            Note(area, ref y, "Key rebinding comes with the final UI.");
        }

        void DrawDisplay(Rect area, ref float y, GameSettings o, SettingsService settings)
        {
            float left = settings.DisplayRevertSecondsLeft;
            if (left > 0f)
            {
                var banner = new Rect(area.x, y - 2f, area.width, 52f);
                UITheme.InkPanel(banner);
                GUI.Label(new Rect(banner.x + 18f, banner.y, banner.width - 300f, banner.height),
                    $"Keep these display settings? Reverting in {Mathf.CeilToInt(left)} s", bannerText);
                if (UITheme.BrushButton(new Rect(banner.xMax - 272f, banner.y + 6f, 124f, 40f), "Keep", light: true)) settings.KeepDisplay();
                if (UITheme.BrushButton(new Rect(banner.xMax - 140f, banner.y + 6f, 124f, 40f), "Revert", light: true)) settings.RevertDisplay();
                y += 62f;
            }

            int mode = System.Array.IndexOf(WindowModes, Screen.fullScreenMode);
            if (mode < 0) mode = 0;
            int newMode = OptionsRow(area, ref y, "Window mode", mode, WindowModeNames);

            int res = resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height));
            if (res < 0) res = resolutions.Count - 1;
            string resText = res >= 0 ? $"{resolutions[res].x} × {resolutions[res].y}" : $"{Screen.width} × {Screen.height}";
            Label(area, y, "Resolution");
            int newRes = UITheme.PaperStepper(new Rect(ControlX(area), y + 6f, 380f, 42f), res, resolutions.Count, resText, resolutions.Count > 1);
            y += RowHeight;

            if ((newMode != mode || newRes != res) && newRes >= 0)
                settings.SetDisplay(WindowModes[newMode], resolutions[newRes].x, resolutions[newRes].y);

            o.vSync = ToggleRow(area, ref y, "VSync", o.vSync);
            int cap = System.Array.IndexOf(FrameCaps, o.frameCap);
            o.frameCap = FrameCaps[OptionsRow(area, ref y, "Frame rate limit", cap < 0 ? FrameCaps.Length - 1 : cap, FrameCapNames, !o.vSync)];
            o.renderScale = SliderRow(area, ref y, "Render scale", o.renderScale, 0.5f, 1f, 0.05f, $"{o.renderScale * 100f:0}%");
            o.shadowQuality = OptionsRow(area, ref y, "Shadows", o.shadowQuality, ShadowNames);
            int aa = System.Array.IndexOf(AntiAliasingSamples, o.antiAliasing);
            o.antiAliasing = AntiAliasingSamples[OptionsRow(area, ref y, "Anti-aliasing", aa < 0 ? 0 : aa, AntiAliasingNames)];
            if (Application.isEditor) Note(area, ref y, "In the Editor, window mode and resolution follow the Game view.");
        }

        void DrawAudio(Rect area, ref float y, GameSettings o)
        {
            o.masterVolume = SliderRow(area, ref y, "Master volume", o.masterVolume, 0f, 1f, 0.05f, $"{o.masterVolume * 100f:0}%");
            o.musicVolume = SliderRow(area, ref y, "Music", o.musicVolume, 0f, 1f, 0.05f, $"{o.musicVolume * 100f:0}%");
            o.effectsVolume = SliderRow(area, ref y, "Sound effects", o.effectsVolume, 0f, 1f, 0.05f, $"{o.effectsVolume * 100f:0}%");
            o.ambienceVolume = SliderRow(area, ref y, "Ambience", o.ambienceVolume, 0f, 1f, 0.05f, $"{o.ambienceVolume * 100f:0}%");
            o.interfaceVolume = SliderRow(area, ref y, "Interface sounds", o.interfaceVolume, 0f, 1f, 0.05f, $"{o.interfaceVolume * 100f:0}%");
            Note(area, ref y, "All sounds are placeholders for now, and there's no music yet.");
        }

        void DrawInterface(Rect area, ref float y, GameSettings o)
        {
            o.uiScale = SliderRow(area, ref y, "Interface size", o.uiScale, 0.8f, 1.2f, 0.05f, $"{o.uiScale * 100f:0}%");
            o.damageNumbers = ToggleRow(area, ref y, "Damage numbers", o.damageNumbers);
            o.cameraShake = SliderRow(area, ref y, "Camera shake", o.cameraShake, 0f, 1f, 0.05f, o.cameraShake <= 0f ? "Off" : $"{o.cameraShake * 100f:0}%");
            Note(area, ref y, "Damage numbers off still shows PARRY! and GUARD BREAK.");
        }

        // ---------- Rows ----------

        static void EnsureStyles()
        {
            if (rowLabel != null && rowLabel.font == UITheme.PaperBody.font) return;
            rowLabel = new GUIStyle(UITheme.PaperBody) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            valueLabel = new GUIStyle(rowLabel) { normal = { textColor = UITheme.InkGoldDark } };
            bannerText = new GUIStyle(UITheme.InkBody) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
        }

        static float ControlX(Rect area) => area.x + LabelWidth;

        static void Label(Rect area, float y, string text) =>
            GUI.Label(new Rect(area.x + 4f, y, LabelWidth - 14f, RowHeight - 2f), text, rowLabel);

        static float SliderRow(Rect area, ref float y, string label, float value, float min, float max, float step, string valueText)
        {
            Label(area, y, label);
            float x = ControlX(area);
            value = UITheme.PaperSlider(new Rect(x, y + 6f, 340f, 40f), value, min, max, step);
            GUI.Label(new Rect(x + 356f, y, 110f, RowHeight - 2f), valueText, valueLabel);
            y += RowHeight;
            return value;
        }

        static bool ToggleRow(Rect area, ref float y, string label, bool value)
        {
            Label(area, y, label);
            value = UITheme.PaperToggle(new Rect(ControlX(area), y + 6f, 200f, 40f), value, value ? "On" : "Off");
            y += RowHeight;
            return value;
        }

        static int OptionsRow(Rect area, ref float y, string label, int selected, string[] options, bool enabled = true)
        {
            Label(area, y, enabled ? label : $"<color={UITheme.MutedOnPaperHex}>{label}</color>");
            float width = Mathf.Min(area.xMax - ControlX(area), options.Length * 120f);
            selected = UITheme.PaperOptions(new Rect(ControlX(area), y + 6f, width, 40f), selected, options, enabled);
            y += RowHeight;
            return selected;
        }

        static void Note(Rect area, ref float y, string text)
        {
            GUI.Label(new Rect(area.x + 4f, y + 6f, area.width - 8f, 24f), text, UITheme.PaperMuted);
            y += 34f;
        }
    }
}
