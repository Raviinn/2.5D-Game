using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Pause menu (Esc): Resume, Save and Load (the active save slot, with its last save time), Controls reference,
    /// Settings (the shared SettingsPanel), Quit to main menu and Quit to desktop (both confirmed).
    /// Prototype IMGUI on the shared UITheme.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        static readonly (string key, string action)[] Controls =
        {
            ("WASD", "Move"), ("Mouse", "Look"), ("Shift (hold)", "Sprint"), ("Shift (tap)", "Dodge"), ("Space", "Jump"),
            ("Space at a ledge", "Grab on (jump toward it) · Space: climb up · A/D: shimmy"), ("Space at ivy", "Climb the wall · Shift: let go"),
            ("LMB", "Attack (hold: charged)"), ("RMB", "Block · tap just before a hit to parry"), ("MMB", "Lock on"),
            ("E / Q", "Skills"), ("X", "Swap weapon"), ("F", "Interact (hold to work a row of soil)"), ("R", "Eat / drink"),
            ("V", "Switch seeds"), ("Tab / I", "Inventory"), ("C", "Character"), ("J", "Journal"), ("M", "Map"),
            ("T", "Switch tracked quest"), ("Esc", "Pause / close menus"),
        };

        GameStateService state;
        SaveService save;
        WorldClock clock;
        SceneLoader loader;
        bool showControls;
        readonly SettingsPanel settingsPanel = new();
        QuitTarget confirmQuit;
        string message;
        float messageTime;

        enum QuitTarget { None, MainMenu, Desktop }

        void OnEnable() => EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
        void OnDisable() => EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);

        void Start()
        {
            state = Services.Get<GameStateService>();
            Services.TryGet(out save);
            Services.TryGet(out clock);
            Services.TryGet(out loader);
        }

        void OnStateChanged(GameStateChangedEvent evt)
        {
            if (settingsPanel.IsOpen) settingsPanel.Close();
            if (evt.Current != GameState.Paused) return;
            confirmQuit = QuitTarget.None;
            message = null;
        }

        void OnGUI()
        {
            if (state == null || state.Current != GameState.Paused) return;
            UITheme.Begin(-2);
            if (settingsPanel.IsOpen) settingsPanel.Draw();
            state.HoldPause = settingsPanel.IsOpen; // Esc closes Settings first, then resumes
            if (settingsPanel.IsOpen) return;

            float width = showControls ? 900f : 420f;
            int slot = save != null ? save.ActiveSlot : 0;
            string subtitle = clock != null ? $"Slot {slot + 1} · Day {clock.Day} · {clock.Hour:00}:{clock.Minute:00}" : null;
            bool hasMainMenu = loader != null && loader.HasMainMenu;
            var area = UITheme.Window(width, hasMainMenu ? 720f : 660f, "Paused", subtitle, $"<color={UITheme.MutedHex}>Esc  resume</color>");

            float x = area.x + 10f;
            float y = area.y + 10f;
            const float buttonWidth = 340f;
            const float buttonHeight = 44f;
            const float gap = 12f;

            if (UITheme.Button(new Rect(x, y, buttonWidth, buttonHeight), "Resume", primary: true)) state.SetState(GameState.Playing);
            y += buttonHeight + gap;

            if (UITheme.Button(new Rect(x, y, buttonWidth, buttonHeight), "Save game", save != null))
            {
                bool saved = save.Save();
                Show(saved ? "Game saved." : "Couldn't save right now.");
            }
            y += buttonHeight + gap;

            bool hasSave = save != null && save.HasSave(slot);
            if (UITheme.Button(new Rect(x, y, buttonWidth, buttonHeight), "Load last save", hasSave))
            {
                state.SetState(GameState.Playing); // loading is only allowed while playing or paused; resume first
                save.Load();
            }
            DateTime? savedAt = save != null ? save.SavedAt(slot) : null;
            GUI.Label(new Rect(x, y + buttonHeight + 2f, buttonWidth, 18f),
                savedAt.HasValue ? $"<color={UITheme.MutedHex}>Last saved {savedAt.Value:MMM d, HH:mm}</color>" : $"<color={UITheme.MutedHex}>No save yet. Sleep in a bed or save here.</color>",
                UITheme.Small);
            y += buttonHeight + gap + 20f;

            if (UITheme.Button(new Rect(x, y, buttonWidth, buttonHeight), showControls ? "Hide controls" : "Controls")) showControls = !showControls;
            y += buttonHeight + gap;

            if (UITheme.Button(new Rect(x, y, buttonWidth, buttonHeight), "Settings"))
            {
                settingsPanel.Open();
                state.HoldPause = true;
            }
            y += buttonHeight + gap;

            if (hasMainMenu)
            {
                QuitButton(new Rect(x, y, buttonWidth, buttonHeight), QuitTarget.MainMenu, "Quit to main menu");
                y += buttonHeight + gap + (confirmQuit == QuitTarget.MainMenu ? 20f : 0f);
            }
            QuitButton(new Rect(x, y, buttonWidth, buttonHeight), QuitTarget.Desktop, "Quit to desktop");

            if (message != null && Time.unscaledTime - messageTime < 3f)
                GUI.Label(new Rect(x, area.yMax - 24f, buttonWidth, 22f), $"<color={UITheme.GoodHex}>{message}</color>", UITheme.Small);

            if (showControls) DrawControls(new Rect(area.x + buttonWidth + 40f, area.y, area.width - buttonWidth - 40f, area.height));
        }

        /// <summary>A quit button that asks first: "Unsaved progress will be lost", then Quit / Cancel.</summary>
        void QuitButton(Rect rect, QuitTarget target, string label)
        {
            if (confirmQuit != target)
            {
                if (UITheme.Button(rect, label)) confirmQuit = target;
                return;
            }

            GUI.Label(new Rect(rect.x, rect.y - 2f, rect.width, 20f), $"<color={UITheme.BadHex}>{label}? Unsaved progress will be lost.</color>", UITheme.Small);
            float half = rect.width * 0.5f - 6f;
            if (UITheme.Button(new Rect(rect.x, rect.y + 20f, half, rect.height - 6f), "Quit"))
            {
                confirmQuit = QuitTarget.None;
                if (target == QuitTarget.MainMenu) loader.LoadMainMenu();
                else Quit();
            }
            if (UITheme.Button(new Rect(rect.x + half + 12f, rect.y + 20f, half, rect.height - 6f), "Cancel")) confirmQuit = QuitTarget.None;
        }

        void DrawControls(Rect area)
        {
            UITheme.Inset(area);
            GUI.Label(new Rect(area.x + 16f, area.y + 10f, area.width - 32f, 26f), "Controls", UITheme.Header);
            float y = area.y + 44f;
            foreach (var (key, action) in Controls)
            {
                GUI.Label(new Rect(area.x + 16f, y, 130f, 22f), $"<color={UITheme.GoldHex}><b>{key}</b></color>", UITheme.Small);
                GUI.Label(new Rect(area.x + 150f, y, area.width - 166f, 22f), action, UITheme.Small);
                y += 22f;
            }
        }

        void Show(string text)
        {
            message = text;
            messageTime = Time.unscaledTime;
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
