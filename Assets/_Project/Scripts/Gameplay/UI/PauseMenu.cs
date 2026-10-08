using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The Options tab of the game menu — the pause (Esc), after Ghost of Tsushima's options page:
    /// - four tiles: Controls, Display, Audio, Interface. A tile opens that category inline (Esc or ‹ returns);
    ///   Controls also lists every key.
    /// - brush buttons: Resume, Save game, Load last save, Quit to title, Quit to desktop (the quits ask first).
    /// GameMenu draws the frame and runs this tab in the Paused state (saving and loading need it).
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour, IGameMenuTab
    {
        static readonly (string key, string action)[] Controls =
        {
            ("WASD", "Move"), ("Mouse", "Look"), ("Shift (hold)", "Sprint"), ("Shift (tap)", "Dodge"), ("Space", "Jump"),
            ("Space at a ledge", "Grab on · Space: climb up · A/D: shimmy"), ("Space at ivy", "Climb the wall · Shift: let go"),
            ("LMB", "Attack (hold: charged)"), ("RMB", "Block · tap just before a hit to parry"), ("MMB", "Lock on"),
            ("E / Q", "Skills"), ("X", "Swap weapon"), ("F", "Interact (hold to work a row of soil)"), ("R", "Eat / drink"),
            ("V", "Switch seeds"), ("Tab / I", "Bag"), ("C", "Character"), ("J", "Journal"), ("M", "Map"),
            ("T", "Switch tracked quest"), ("Esc", "Options · close menus"), ("Q / E in menus", "Switch tab"),
        };

        static readonly string[] TileBlurbs =
        {
            "Look speed, invert, every key",
            "Window, resolution, shadows",
            "Volume for each kind of sound",
            "Size, damage numbers, shake",
        };

        enum QuitTarget { None, MainMenu, Desktop }

        GameStateService state;
        SaveService save;
        WorldClock clock;
        SceneLoader loader;
        readonly SettingsPanel settingsPanel = new();
        QuitTarget confirmQuit;
        bool guideOpen;
        Vector2 guideScroll;
        string message;
        float messageTime;
        GUIStyle tileLabel, tileBlurb, keyStyle, actionStyle, statusStyle;

        void Start()
        {
            state = Services.Get<GameStateService>();
            Services.TryGet(out save);
            Services.TryGet(out clock);
            Services.TryGet(out loader);
        }

        // ---------- Game menu tab ----------

        public bool CanOpen => true;

        public string FooterTip
        {
            get
            {
                if (clock == null) return "Settings are shared by every save slot";
                int slot = save != null ? save.ActiveSlot : 0;
                return $"Slot {slot + 1}  ·  Day {clock.Day}  ·  {clock.Hour:00}:{clock.Minute:00}";
            }
        }

        public (string key, string label)[] KeyHints => null;

        /// <summary>A settings category or a quit confirm is open (Esc closes it before the menu).</summary>
        public bool HasSubView => settingsPanel.IsOpen || confirmQuit != QuitTarget.None || guideOpen;
        /// <summary>The "How to play" page is showing (Milestone 53).</summary>
        public bool GuideOpen => guideOpen;

        public void CloseSubView()
        {
            if (confirmQuit != QuitTarget.None) confirmQuit = QuitTarget.None;
            else if (settingsPanel.IsOpen) settingsPanel.Close();
            else if (guideOpen) guideOpen = false;
        }

        public void OnTabOpened()
        {
            if (settingsPanel.IsOpen) settingsPanel.Close();
            confirmQuit = QuitTarget.None;
            guideOpen = false;
            message = null;
        }

        /// <summary>Opens the "How to play" page. Tests use it.</summary>
        public void OpenGuide() => guideOpen = true;

        /// <summary>Opens a settings category inline (0 Controls, 1 Display, 2 Audio, 3 Interface). Tests use it.</summary>
        public void OpenCategory(int category) => settingsPanel.Open(category);

        public void DrawTab(Rect content)
        {
            if (state == null) Start();
            EnsureStyles();
            if (settingsPanel.IsOpen) DrawCategory(content);
            else if (guideOpen) DrawGuide(content);
            else DrawHome(content);
        }

        // ---------- Home: tiles and buttons ----------

        void DrawHome(Rect content)
        {
            const float gap = 18f;
            float tilesWidth = Mathf.Min(content.width, 1240f);
            float tileWidth = (tilesWidth - gap * 3f) / 4f;
            float tileHeight = Mathf.Min(220f, content.height * 0.34f);
            float x0 = content.center.x - tilesWidth * 0.5f;
            for (int i = 0; i < SettingsPanel.CategoryNames.Length; i++)
            {
                var tile = new Rect(x0 + i * (tileWidth + gap), content.y, tileWidth, tileHeight);
                bool hovered = tile.Contains(Event.current.mousePosition);
                UITheme.PaperCard(tile, hovered);
                tileLabel.normal.textColor = hovered ? UITheme.OffWhite : UITheme.Ink;
                tileBlurb.normal.textColor = hovered ? UITheme.OffWhite : UITheme.MutedOnPaper;
                GUI.Label(new Rect(tile.x + 22f, tile.yMax - 76f, tile.width - 44f, 34f), UITheme.Spaced(SettingsPanel.CategoryNames[i]), tileLabel);
                GUI.Label(new Rect(tile.x + 22f, tile.yMax - 40f, tile.width - 44f, 24f), TileBlurbs[i], tileBlurb);
                DrawTileMark(tile, i, hovered);
                if (UITheme.PaperClick(tile)) settingsPanel.Open(i);
            }

            const float buttonWidth = 420f;
            const float buttonHeight = 50f;
            const float spacing = 14f;
            float x = content.center.x - buttonWidth * 0.5f;
            float y = content.y + tileHeight + 40f;
            int slot = save != null ? save.ActiveSlot : 0;

            if (UITheme.BrushButton(new Rect(x, y, buttonWidth, buttonHeight), "Resume")) state.SetState(GameState.Playing);
            y += buttonHeight + spacing;

            if (UITheme.BrushButton(new Rect(x, y, buttonWidth, buttonHeight), "How to play")) guideOpen = true;
            y += buttonHeight + spacing;

            if (UITheme.BrushButton(new Rect(x, y, buttonWidth, buttonHeight), "Save game", save != null))
                Show(save.Save() ? "Game saved." : "Couldn't save right now.");
            y += buttonHeight + spacing;

            bool hasSave = save != null && save.HasSave(slot);
            if (UITheme.BrushButton(new Rect(x, y, buttonWidth, buttonHeight), "Load last save", hasSave))
            {
                state.SetState(GameState.Playing); // loading is only allowed while playing or paused; resume first
                save.Load();
            }
            DateTime? savedAt = save != null ? save.SavedAt(slot) : null;
            GUI.Label(new Rect(x, y + buttonHeight + 2f, buttonWidth, 22f),
                savedAt.HasValue ? $"Last saved {savedAt.Value:MMM d, HH:mm}" : "No save yet. Sleep in a bed or save here.", statusStyle);
            y += buttonHeight + spacing + 24f;

            if (loader != null && loader.HasMainMenu)
            {
                QuitButton(new Rect(x, y, buttonWidth, buttonHeight), QuitTarget.MainMenu, "Quit to title");
                y += buttonHeight + spacing + (confirmQuit == QuitTarget.MainMenu ? 26f : 0f);
            }
            QuitButton(new Rect(x, y, buttonWidth, buttonHeight), QuitTarget.Desktop, "Quit to desktop");
            y += buttonHeight + spacing + (confirmQuit == QuitTarget.Desktop ? 26f : 0f);

            if (message != null && Time.unscaledTime - messageTime < 3f)
                GUI.Label(new Rect(x, y, buttonWidth, 24f), $"<color={UITheme.InkGoldDarkHex}>{message}</color>", statusStyle);
        }

        /// <summary>A small ink mark in the tile's top-left corner, so the four tiles read apart at a glance.</summary>
        static void DrawTileMark(Rect tile, int index, bool hovered)
        {
            var icon = index switch { 0 => UITheme.DiamondIcon, 1 => UITheme.BoxIcon, 2 => UITheme.RingIcon, _ => UITheme.CircleIcon };
            UITheme.DrawIcon(new Rect(tile.x + 22f, tile.y + 22f, 28f, 28f), icon, hovered ? UITheme.OffWhite : UITheme.Ink);
        }

        /// <summary>A quit button that asks first: "Unsaved progress will be lost", then Quit / Cancel.</summary>
        void QuitButton(Rect rect, QuitTarget target, string label)
        {
            if (confirmQuit != target)
            {
                if (UITheme.BrushButton(rect, label)) confirmQuit = target;
                return;
            }

            GUI.Label(new Rect(rect.x, rect.y - 4f, rect.width, 24f),
                $"<color={UITheme.VermilionHex}>{label}? Unsaved progress will be lost.</color>", statusStyle);
            float half = rect.width * 0.5f - 6f;
            if (UITheme.BrushButton(new Rect(rect.x, rect.y + 22f, half, rect.height), "Quit"))
            {
                confirmQuit = QuitTarget.None;
                if (target == QuitTarget.MainMenu) loader.LoadMainMenu();
                else Quit();
            }
            if (UITheme.BrushButton(new Rect(rect.x + half + 12f, rect.y + 22f, half, rect.height), "Cancel")) confirmQuit = QuitTarget.None;
        }

        // ---------- How to play (Milestone 53) ----------

        static (string title, string body)[] GuideSections() => new[]
        {
            ("The farm", $"Till grass with [{UITheme.KeyLabel("F")}], plant seeds, water them. Watered crops grow one stage a night; two dry days and they wilt, three and they die. Rain waters everything. [{UITheme.KeyLabel("V")}] switches seeds. Hold [{UITheme.KeyLabel("F")}] to work a whole row."),
            ("Seasons", "Each season lasts 14 days. Turnips and Healroot grow spring to autumn, pumpkins in autumn, frost kale in winter. The change of season kills what can't grow in it. Spring 8 is the Planting Festival; the last day of autumn is the Harvest Fair."),
            ("Fighting", $"[{UITheme.KeyLabel("LMB")}] attacks (hold for a heavy blow), [{UITheme.KeyLabel("RMB")}] blocks; tap it just before a hit to parry. [{UITheme.KeyLabel("Shift")}] dodges through attacks. Enemies glow before they swing. Shieldbearers block from the front: go round. Wolves circle behind you."),
            ("Night", "After dark bandits hit harder and carry more, and something big walks the dead wood. Stay up too long and you tire. Sleep in your bed to skip to morning: it saves the game."),
            ("Town", "Talk to people for quests and trade. The contracts board posts new work every day. Helping the Free Hollows raises your standing: better prices, more contracts, gifts."),
            ("Homestead", "The workbench brews, cooks, smiths and mends worn gear. The chest stores what you don't carry. Feed the animals at the trough for eggs and milk. The plans by the bed list improvements to build."),
            ("Out and about", "Wild food grows back a few days after you gather it. Buy a rod from Oswin and fish at the pond: press on the bite, then each time the needle crosses the gold."),
        };

        void DrawGuide(Rect content)
        {
            if (UITheme.BrushButton(new Rect(content.x, content.y, 64f, 46f), "‹")) guideOpen = false;
            GUI.Label(new Rect(content.x + 84f, content.y, 600f, 46f), UITheme.Spaced("How to play"), UITheme.InkHeader);
            UITheme.Fill(new Rect(content.x, content.y + 60f, content.width, 1f), new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.4f));

            var view = new Rect(content.x, content.y + 78f, content.width, content.height - 78f);
            var sections = GuideSections();
            float column = (view.width - 18f - 24f) * 0.5f;
            const float cardHeight = 150f;
            int rows = (sections.Length + 1) / 2;
            var inner = new Rect(0f, 0f, view.width - 18f, rows * (cardHeight + 16f));
            guideScroll = UITheme.BeginScroll(view, guideScroll, inner);
            for (int i = 0; i < sections.Length; i++)
            {
                var card = new Rect(i % 2 * (column + 24f), i / 2 * (cardHeight + 16f), column, cardHeight);
                UITheme.PaperCard(card, false);
                GUI.Label(new Rect(card.x + 20f, card.y + 12f, card.width - 40f, 30f), UITheme.Spaced(sections[i].title), UITheme.InkHeader);
                GUI.Label(new Rect(card.x + 20f, card.y + 46f, card.width - 40f, card.height - 56f), sections[i].body, UITheme.PaperBody);
            }
            UITheme.EndScroll(ref guideScroll, view);
        }

        // ---------- A category, inline ----------

        void DrawCategory(Rect content)
        {
            int category = settingsPanel.Category;
            if (UITheme.BrushButton(new Rect(content.x, content.y, 64f, 46f), "‹")) settingsPanel.Close();
            GUI.Label(new Rect(content.x + 84f, content.y, 600f, 46f), UITheme.Spaced(SettingsPanel.CategoryNames[category]), UITheme.InkHeader);
            UITheme.Fill(new Rect(content.x, content.y + 60f, content.width, 1f), new Color(UITheme.Ink.r, UITheme.Ink.g, UITheme.Ink.b, 0.4f));

            var body = new Rect(content.x, content.y + 78f, content.width, content.height - 78f);
            if (category == 0)
            {
                float leftWidth = Mathf.Min(760f, body.width * 0.52f);
                settingsPanel.DrawCategory(new Rect(body.x, body.y, leftWidth, body.height - 70f), category);
                DrawKeys(new Rect(body.x + leftWidth + 40f, body.y, body.width - leftWidth - 40f, body.height));
            }
            else
            {
                settingsPanel.DrawCategory(new Rect(body.x, body.y, Mathf.Min(body.width, 960f), body.height - 70f), category);
            }
            settingsPanel.DrawReset(new Rect(body.x, body.yMax - 50f, 320f, 50f));
        }

        void DrawKeys(Rect area)
        {
            UITheme.PaperCard(area, false);
            GUI.Label(new Rect(area.x + 22f, area.y + 14f, area.width - 44f, 34f), UITheme.Spaced("Keys"), UITheme.InkHeader);
            float y = area.y + 58f;
            float rowHeight = Mathf.Min(28f, (area.height - 72f) / Controls.Length);
            foreach (var (key, action) in Controls)
            {
                GUI.Label(new Rect(area.x + 22f, y, 170f, rowHeight), WithRebinds(key), keyStyle);
                GUI.Label(new Rect(area.x + 196f, y, area.width - 218f, rowHeight), action, actionStyle);
                y += rowHeight;
            }
        }

        // ---------- Helpers ----------

        /// <summary>"F" → "H" once Interact is rebound (Milestone 50); each default key in the text is swapped.</summary>
        static string WithRebinds(string keys)
        {
            foreach (var row in KeyBindings.Rows)
            {
                if (string.IsNullOrEmpty(row.DefaultKey)) continue;
                string now = KeyBindings.Rebound(row.DefaultKey, false);
                if (now == null) continue;
                keys = System.Text.RegularExpressions.Regex.Replace(keys, $@"(?<![A-Za-z]){System.Text.RegularExpressions.Regex.Escape(row.DefaultKey)}(?![A-Za-z])", now);
            }
            return keys;
        }

        void EnsureStyles()
        {
            if (tileLabel != null && tileLabel.font == UITheme.InkHeader.font) return;
            tileLabel = new GUIStyle(UITheme.InkHeader) { fontSize = 22 };
            tileBlurb = new GUIStyle(UITheme.PaperMuted) { wordWrap = false };
            keyStyle = new GUIStyle(UITheme.PaperBody) { wordWrap = false, alignment = TextAnchor.MiddleLeft, normal = { textColor = UITheme.InkGoldDark } };
            actionStyle = new GUIStyle(UITheme.PaperBody) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            statusStyle = new GUIStyle(UITheme.PaperMuted) { wordWrap = false, alignment = TextAnchor.MiddleCenter };
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
