using System.Collections.Generic;
using Beast.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Gameplay
{
    /// <summary>
    /// The game menu (UI restyle step 2): one parchment screen with tabs on an ink top bar — Map · Journal · Bag ·
    /// Character · Options — after Ghost of Tsushima's menu.
    /// - Opening: M / J / Tab or I / C open their tab; Esc opens Options (the pause: GameStateService sets Paused).
    /// - Inside: Q / E (or the shoulder buttons) switch tabs; another tab's key jumps to it, the current tab's key closes;
    ///   Esc closes, or first backs out of a tab's sub-view (a settings category, a confirm).
    /// - Options runs in the Paused state (saving and loading need it); the other tabs in InGameMenu. Time is frozen in both.
    /// The tab screens (Minimap, QuestJournal, InventoryScreen, CharacterScreen, PauseMenu) draw only their content.
    /// </summary>
    public sealed class GameMenu : MonoBehaviour
    {
        const float TopBarHeight = 64f;
        static readonly string[] TabNames = { "Map", "Journal", "Bag", "Character", "Options" };

        readonly IGameMenuTab[] tabs = new IGameMenuTab[TabNames.Length];
        GameStateService state;
        InputAction inventoryAction, characterAction, journalAction, mapAction;
        Inventory inventory;
        PlayerProgression progression;
        int openedFrame = -1;
        GUIStyle barRight;

        public bool IsOpen { get; private set; }
        public GameMenuTab ActiveTab { get; private set; } = GameMenuTab.Options;
        public IGameMenuTab Current => tabs[(int)ActiveTab];

        void OnEnable() => EventBus<GameStateChangedEvent>.Subscribe(OnStateChanged);
        void OnDisable() => EventBus<GameStateChangedEvent>.Unsubscribe(OnStateChanged);

        void Start()
        {
            state = Services.Get<GameStateService>();
            var input = Services.Get<InputService>();
            inventoryAction = input.Inventory;
            characterAction = input.Character;
            journalAction = input.Journal;
            mapAction = input.Map;
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                inventory = player.GetComponent<Inventory>();
                progression = player.GetComponent<PlayerProgression>();
            }
            FindTabs();
        }

        void FindTabs()
        {
            tabs[(int)GameMenuTab.Map] = FindFirstObjectByType<Minimap>();
            tabs[(int)GameMenuTab.Journal] = FindFirstObjectByType<QuestJournal>();
            tabs[(int)GameMenuTab.Bag] = FindFirstObjectByType<InventoryScreen>();
            tabs[(int)GameMenuTab.Character] = FindFirstObjectByType<CharacterScreen>();
            tabs[(int)GameMenuTab.Options] = FindFirstObjectByType<PauseMenu>();
        }

        bool Available(GameMenuTab tab)
        {
            var t = tabs[(int)tab];
            return t != null && t.CanOpen;
        }

        // ---------- Opening, switching, closing ----------

        /// <summary>Opens the menu at this tab (or switches to it). Does nothing if the tab can't open.</summary>
        public void Open(GameMenuTab tab)
        {
            if (state == null) Start();
            if (!Available(tab)) return;
            if (!IsOpen && state.Current is not (GameState.Playing or GameState.Paused or GameState.InGameMenu)) return;

            if (IsOpen) Current?.CloseSubView();
            ActiveTab = tab;
            IsOpen = true;
            openedFrame = Time.frameCount;
            state.SetState(tab == GameMenuTab.Options ? GameState.Paused : GameState.InGameMenu);
            state.BlockHotkeyClose = true; // this menu reads its own hotkeys (switch tabs instead of closing)
            Current.OnTabOpened();
        }

        public void Close()
        {
            if (!IsOpen) return;
            Current?.CloseSubView();
            IsOpen = false;
            if (state.Current is GameState.Paused or GameState.InGameMenu) state.SetState(GameState.Playing);
        }

        void Step(int direction)
        {
            for (int i = 1; i <= tabs.Length; i++)
            {
                var next = (GameMenuTab)(((int)ActiveTab + direction * i + tabs.Length * 2) % tabs.Length);
                if (!Available(next)) continue;
                Open(next);
                return;
            }
        }

        /// <summary>A tab's hotkey inside the menu: jump to that tab, or close if it's already showing.</summary>
        void Hotkey(GameMenuTab tab)
        {
            if (ActiveTab == tab) Close();
            else Open(tab);
        }

        void OnStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current == GameState.Paused && !IsOpen)
            {
                // Esc in the world (GameStateService paused the game): the menu opens on Options.
                if (state == null) Start();
                if (!Available(GameMenuTab.Options)) return;
                ActiveTab = GameMenuTab.Options;
                IsOpen = true;
                openedFrame = Time.frameCount;
                Current.OnTabOpened();
                return;
            }
            if (IsOpen && evt.Current is not (GameState.Paused or GameState.InGameMenu))
            {
                Current?.CloseSubView();
                IsOpen = false;
            }
        }

        void Update()
        {
            if (state == null) return;

            if (!IsOpen)
            {
                if (state.Current != GameState.Playing) return;
                if (inventoryAction.WasPressedThisFrame()) Open(GameMenuTab.Bag);
                else if (characterAction.WasPressedThisFrame()) Open(GameMenuTab.Character);
                else if (journalAction.WasPressedThisFrame()) Open(GameMenuTab.Journal);
                else if (mapAction.WasPressedThisFrame()) Open(GameMenuTab.Map);
                return;
            }

            if (state.Current == GameState.InGameMenu) state.BlockHotkeyClose = true;
            state.HoldPause = Current != null && Current.HasSubView; // Esc backs out of the sub-view first
            if (Time.frameCount == openedFrame) return; // the key that opened the menu mustn't also act inside it

            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            // Esc (or the gamepad's B / Start) with a sub-view open backs out of it; HoldPause kept the menu open.
            bool back = (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                        (pad != null && (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
            if (back && Current != null && Current.HasSubView)
            {
                Current.CloseSubView();
                return;
            }

            if ((keyboard != null && keyboard.eKey.wasPressedThisFrame) || (pad != null && pad.rightShoulder.wasPressedThisFrame)) Step(1);
            else if ((keyboard != null && keyboard.qKey.wasPressedThisFrame) || (pad != null && pad.leftShoulder.wasPressedThisFrame)) Step(-1);
            else if (keyboard != null && (keyboard.tabKey.wasPressedThisFrame || keyboard.iKey.wasPressedThisFrame)) Hotkey(GameMenuTab.Bag);
            else if (pad != null && pad.selectButton.wasPressedThisFrame) Hotkey(GameMenuTab.Bag);
            else if (keyboard != null && keyboard.cKey.wasPressedThisFrame) Hotkey(GameMenuTab.Character);
            else if (keyboard != null && keyboard.jKey.wasPressedThisFrame) Hotkey(GameMenuTab.Journal);
            else if (keyboard != null && keyboard.mKey.wasPressedThisFrame) Hotkey(GameMenuTab.Map);
        }

        // ---------- Drawing ----------

        void OnGUI()
        {
            if (!IsOpen || state == null || state.Current is not (GameState.Paused or GameState.InGameMenu)) return;
            UITheme.Begin(-3);
            barRight ??= new GUIStyle(UITheme.InkSmall) { alignment = TextAnchor.MiddleRight };

            float w = UITheme.Width, h = UITheme.Height;
            UITheme.ParchmentBackground(new Rect(0f, 0f, w, h));
            DrawTopBar(w);

            var content = new Rect(48f, TopBarHeight + 26f, w - 96f, h - TopBarHeight - 26f - 92f);
            Current?.DrawTab(content);

            var tab = Current;
            if (tab != null && !string.IsNullOrEmpty(tab.FooterTip))
                UITheme.FooterTip(new Rect(-30f, h - 74f, Mathf.Min(820f, w * 0.52f), 48f), tab.FooterTip);
            var hints = new List<(string, string)>();
            if (tab?.KeyHints != null) hints.AddRange(tab.KeyHints);
            hints.Add(("Q / E", "Switch"));
            hints.Add(("Esc", tab != null && tab.HasSubView ? "Back" : "Close"));
            UITheme.KeyHints(w - 48f, h - 64f, false, hints.ToArray());
        }

        void DrawTopBar(float w)
        {
            UITheme.Fill(new Rect(0f, 0f, w, TopBarHeight), UITheme.Ink);
            float x = 40f;
            x = KeyChip(x, UITheme.UsingGamepad ? "LB" : "Q") + 18f;

            var evt = Event.current;
            for (int i = 0; i < TabNames.Length; i++)
            {
                var tab = (GameMenuTab)i;
                bool available = Available(tab);
                string label = UITheme.Spaced(TabNames[i]);
                float width = UITheme.TabLabel.CalcSize(new GUIContent(label)).x + 44f;
                var rect = new Rect(x, 0f, width, TopBarHeight);
                bool active = tab == ActiveTab;
                bool hovered = available && !active && rect.Contains(evt.mousePosition);

                if (active) UITheme.Fill(rect, UITheme.Parchment);
                else if (hovered) UITheme.Fill(new Rect(rect.x + 10f, rect.yMax - 4f, rect.width - 20f, 3f), UITheme.Vermilion);
                var old = GUI.color;
                if (!available) GUI.color = new Color(1f, 1f, 1f, 0.35f);
                UITheme.TabLabel.normal.textColor = active ? UITheme.Ink : UITheme.OffWhite;
                GUI.Label(rect, label, UITheme.TabLabel);
                UITheme.TabLabel.normal.textColor = UITheme.OffWhite;
                GUI.color = old;
                if (available && !active && UITheme.PointerClick(rect)) Open(tab); // the pad uses LB / RB
                x += width + 4f;
            }
            KeyChip(x + 14f, UITheme.UsingGamepad ? "RB" : "E");

            // Right: renown and gold.
            string renown = progression != null ? $"Renown {progression.Renown}" : string.Empty;
            string gold = inventory != null ? $"{inventory.Gold}" : string.Empty;
            float goldWidth = UITheme.InkSmall.CalcSize(new GUIContent(gold)).x;
            GUI.Label(new Rect(w - 40f - goldWidth, 0f, goldWidth + 2f, TopBarHeight), gold, barRight);
            UITheme.DrawIcon(new Rect(w - 40f - goldWidth - 24f, TopBarHeight * 0.5f - 9f, 18f, 18f), UITheme.CoinIcon, UITheme.InkGold);
            GUI.Label(new Rect(w - 40f - goldWidth - 280f, 0f, 240f, TopBarHeight), renown, barRight);
        }

        /// <summary>A small key cap ("Q") on the ink bar; returns its right edge.</summary>
        static float KeyChip(float x, string key)
        {
            var rect = new Rect(x, TopBarHeight * 0.5f - 14f, 30f, 28f);
            var line = new Color(0.45f, 0.43f, 0.40f);
            UITheme.Fill(new Rect(rect.x, rect.y, rect.width, 1f), line);
            UITheme.Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), line);
            UITheme.Fill(new Rect(rect.x, rect.y, 1f, rect.height), line);
            UITheme.Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), line);
            var old = UITheme.InkSmall.alignment;
            UITheme.InkSmall.alignment = TextAnchor.MiddleCenter;
            GUI.Label(rect, key, UITheme.InkSmall);
            UITheme.InkSmall.alignment = old;
            return rect.xMax;
        }
    }
}
