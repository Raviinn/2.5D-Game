using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The Settings window, shared by the main menu and the pause menu (each owns one and draws it in OnGUI).
    /// Tabs: Controls · Display · Audio · Interface. Every change applies at once and saves itself shortly after
    /// (SettingsService); window mode / resolution changes ask to be kept and revert after a few seconds.
    /// </summary>
    public sealed class SettingsPanel
    {
        enum Tab { Controls, Display, Audio, Interface }

        static readonly string[] TabNames = { "Controls", "Display", "Audio", "Interface" };
        static readonly string[] WindowModeNames = { "Borderless", "Fullscreen", "Windowed" };
        static readonly FullScreenMode[] WindowModes = { FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen, FullScreenMode.Windowed };
        static readonly string[] FrameCapNames = { "30", "60", "120", "144", "No limit" };
        static readonly int[] FrameCaps = { 30, 60, 120, 144, 0 };
        static readonly string[] ShadowNames = { "Off", "Low", "Medium", "High" };
        static readonly string[] AntiAliasingNames = { "Off", "2×", "4×", "8×" };
        static readonly int[] AntiAliasingSamples = { 1, 2, 4, 8 };

        const float Width = 860f;
        const float Height = 660f;
        const float RowHeight = 50f;
        const float LabelWidth = 300f;

        Tab tab;
        bool confirmReset;
        List<Vector2Int> resolutions = new();

        public bool IsOpen { get; private set; }

        public void Open()
        {
            IsOpen = true;
            confirmReset = false;
            resolutions = SettingsService.AvailableResolutions();
        }

        public void Close()
        {
            IsOpen = false;
            if (Services.TryGet(out SettingsService settings))
            {
                settings.KeepDisplay(); // leaving the screen counts as keeping what you see
                settings.Save();
            }
        }

        /// <summary>Draws the window when open (call inside a UITheme.Begin OnGUI). Esc or Back closes it.</summary>
        public void Draw()
        {
            if (!IsOpen) return;
            if (!Services.TryGet(out SettingsService settings))
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

            UITheme.Backdrop(0.35f);
            var panel = new Rect((UITheme.Width - Width) * 0.5f, (UITheme.Height - Height) * 0.5f, Width, Mathf.Min(Height, UITheme.Height - 32f));
            UITheme.Panel(panel);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 14f, 300f, 34f), "Settings", UITheme.Title);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 16f, Width - 48f, 30f),
                $"<color={UITheme.MutedHex}>Changes apply right away and are saved for every slot</color>", UITheme.SmallRight);

            var tabsRect = new Rect(panel.x + 20f, panel.y + 62f, Width - 40f, 38f);
            float tabWidth = (tabsRect.width - 18f) / TabNames.Length;
            for (int i = 0; i < TabNames.Length; i++)
                if (UITheme.Tab(new Rect(tabsRect.x + i * (tabWidth + 6f), tabsRect.y, tabWidth, tabsRect.height), TabNames[i], (int)tab == i))
                    tab = (Tab)i;

            var content = new Rect(panel.x + 20f, tabsRect.yMax + 12f, Width - 40f, panel.height - 66f - 38f - 12f - 80f);
            UITheme.Inset(content);
            var options = settings.Current;
            GUI.changed = false;
            float y = content.y + 14f;
            switch (tab)
            {
                case Tab.Controls: DrawControls(content, ref y, options); break;
                case Tab.Display: DrawDisplay(content, ref y, options, settings); break;
                case Tab.Audio: DrawAudio(content, ref y, options); break;
                case Tab.Interface: DrawInterface(content, ref y, options); break;
            }
            if (GUI.changed) settings.Apply();

            DrawFooter(panel, settings);
        }

        // ---------- Tabs ----------

        void DrawControls(Rect area, ref float y, GameSettings o)
        {
            o.mouseSensitivity = SliderRow(area, ref y, "Mouse sensitivity", o.mouseSensitivity, 0.25f, 3f, 0.05f, $"{o.mouseSensitivity:0.00}×");
            o.stickSensitivity = SliderRow(area, ref y, "Controller look speed", o.stickSensitivity, 0.25f, 3f, 0.05f, $"{o.stickSensitivity:0.00}×");
            o.invertY = ToggleRow(area, ref y, "Invert vertical look", o.invertY);
            Note(area, ref y, "Key rebinding comes with the final UI. The controls list is in the pause menu.");
        }

        void DrawDisplay(Rect area, ref float y, GameSettings o, SettingsService settings)
        {
            float left = settings.DisplayRevertSecondsLeft;
            if (left > 0f)
            {
                var banner = new Rect(area.x + 12f, y - 4f, area.width - 24f, 44f);
                UITheme.Fill(banner, new Color(0.35f, 0.25f, 0.08f, 0.9f));
                GUI.Label(new Rect(banner.x + 14f, banner.y, banner.width - 260f, banner.height),
                    $"Keep these display settings? Reverting in {Mathf.CeilToInt(left)} s", UITheme.BodyMiddle);
                if (UITheme.Button(new Rect(banner.xMax - 236f, banner.y + 6f, 110f, 32f), "Keep", primary: true)) settings.KeepDisplay();
                if (UITheme.Button(new Rect(banner.xMax - 118f, banner.y + 6f, 110f, 32f), "Revert")) settings.RevertDisplay();
                y += 52f;
            }

            int mode = System.Array.IndexOf(WindowModes, Screen.fullScreenMode);
            if (mode < 0) mode = 0;
            int newMode = OptionsRow(area, ref y, "Window mode", mode, WindowModeNames);

            int res = resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height));
            if (res < 0) res = resolutions.Count - 1;
            string resText = res >= 0 ? $"{resolutions[res].x} × {resolutions[res].y}" : $"{Screen.width} × {Screen.height}";
            Label(area, y, "Resolution");
            int newRes = UITheme.Stepper(new Rect(ControlX(area), y + 6f, 360f, 36f), res, resolutions.Count, resText, resolutions.Count > 1);
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
            Note(area, ref y, "The game has no sound yet; these apply as soon as it does.");
        }

        void DrawInterface(Rect area, ref float y, GameSettings o)
        {
            o.uiScale = SliderRow(area, ref y, "Interface size", o.uiScale, 0.8f, 1.2f, 0.05f, $"{o.uiScale * 100f:0}%");
            o.damageNumbers = ToggleRow(area, ref y, "Damage numbers", o.damageNumbers);
            o.cameraShake = SliderRow(area, ref y, "Camera shake", o.cameraShake, 0f, 1f, 0.05f, o.cameraShake <= 0f ? "Off" : $"{o.cameraShake * 100f:0}%");
            Note(area, ref y, "Damage numbers off still shows PARRY! and GUARD BREAK.");
        }

        void DrawFooter(Rect panel, SettingsService settings)
        {
            float y = panel.yMax - 62f;
            if (!confirmReset)
            {
                if (UITheme.Button(new Rect(panel.x + 20f, y, 220f, 42f), "Reset to defaults")) confirmReset = true;
            }
            else
            {
                GUI.Label(new Rect(panel.x + 20f, y - 22f, 420f, 20f), $"<color={UITheme.BadHex}>Reset every setting (not window mode or resolution)?</color>", UITheme.Small);
                if (UITheme.Button(new Rect(panel.x + 20f, y, 110f, 42f), "Reset"))
                {
                    settings.ResetToDefaults();
                    confirmReset = false;
                }
                if (UITheme.Button(new Rect(panel.x + 140f, y, 110f, 42f), "Cancel")) confirmReset = false;
            }

            GUI.Label(new Rect(panel.xMax - 380f, y, 180f, 42f), $"<color={UITheme.MutedHex}>Esc  back</color>", UITheme.SmallRight);
            if (UITheme.Button(new Rect(panel.xMax - 180f, y, 160f, 42f), "Back", primary: true)) Close();
        }

        // ---------- Rows ----------

        static float ControlX(Rect area) => area.x + 20f + LabelWidth;

        static void Label(Rect area, float y, string text) =>
            GUI.Label(new Rect(area.x + 20f, y, LabelWidth - 10f, RowHeight - 2f), text, UITheme.BodyMiddle);

        static float SliderRow(Rect area, ref float y, string label, float value, float min, float max, float step, string valueText)
        {
            Label(area, y, label);
            float x = ControlX(area);
            value = UITheme.Slider(new Rect(x, y + 6f, 330f, 36f), value, min, max, step);
            GUI.Label(new Rect(x + 344f, y, 100f, RowHeight - 2f), $"<color={UITheme.GoldHex}>{valueText}</color>", UITheme.BodyMiddle);
            y += RowHeight;
            return value;
        }

        static bool ToggleRow(Rect area, ref float y, string label, bool value)
        {
            Label(area, y, label);
            value = UITheme.Toggle(new Rect(ControlX(area), y + 6f, 200f, 36f), value, value ? "On" : "Off");
            y += RowHeight;
            return value;
        }

        static int OptionsRow(Rect area, ref float y, string label, int selected, string[] options, bool enabled = true)
        {
            Label(area, y, enabled ? label : $"<color={UITheme.MutedHex}>{label}</color>");
            float width = Mathf.Min(area.xMax - ControlX(area) - 20f, options.Length * 110f);
            selected = UITheme.Options(new Rect(ControlX(area), y + 7f, width, 34f), selected, options, enabled);
            y += RowHeight;
            return selected;
        }

        static void Note(Rect area, ref float y, string text)
        {
            GUI.Label(new Rect(area.x + 20f, y + 4f, area.width - 40f, 22f), $"<color={UITheme.MutedHex}>{text}</color>", UITheme.Small);
            y += 30f;
        }
    }
}
