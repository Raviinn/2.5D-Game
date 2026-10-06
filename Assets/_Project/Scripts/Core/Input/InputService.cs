using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Core
{
    /// <summary>
    /// Wraps the GameControls asset. Enables exactly one action map per game state
    /// (Player / UI / Cutscene) and manages cursor lock.
    /// </summary>
    public sealed class InputService : MonoBehaviour
    {
        InputActionAsset actions;
        InputActionMap playerMap;
        InputActionMap uiMap;
        InputActionMap cutsceneMap;

        public InputActionAsset Actions => actions;

        // Player
        public InputAction Move { get; private set; }
        public InputAction Look { get; private set; }
        public InputAction Sprint { get; private set; }
        public InputAction Jump { get; private set; }
        public InputAction Attack { get; private set; }
        public InputAction Heavy { get; private set; }
        public InputAction Block { get; private set; }
        public InputAction Dodge { get; private set; }
        public InputAction LockOn { get; private set; }
        public InputAction SwitchStyle { get; private set; }
        public InputAction Interact { get; private set; }
        public InputAction Pause { get; private set; }
        public InputAction Inventory { get; private set; }
        public InputAction UseItem { get; private set; }
        public InputAction CycleSeed { get; private set; }
        public InputAction Skill1 { get; private set; }
        public InputAction Skill2 { get; private set; }
        public InputAction Character { get; private set; }
        public InputAction Journal { get; private set; }
        public InputAction Map { get; private set; }
        public InputAction TrackQuest { get; private set; }

        // UI
        /// <summary>Esc / Start: resume from the pause menu, or close any in-game menu.</summary>
        public InputAction Resume { get; private set; }
        /// <summary>Tab / I / C / J / Select: close an in-game menu (the same keys that open them). Never unpauses.</summary>
        public InputAction CloseMenu { get; private set; }
        public InputAction Navigate { get; private set; }
        /// <summary>Continue dialogue / pick a choice (Space, Enter, F, click, A). UI map.</summary>
        public InputAction DialogueAdvance { get; private set; }

        // Cutscene
        public InputAction Skip { get; private set; }
        public InputAction Advance { get; private set; }

        public void Initialize(InputActionAsset source)
        {
            // Work on a runtime copy so callbacks never leak into the project asset between Play sessions.
            actions = Instantiate(source);

            playerMap = actions.FindActionMap("Player", throwIfNotFound: true);
            uiMap = actions.FindActionMap("UI", throwIfNotFound: true);
            cutsceneMap = actions.FindActionMap("Cutscene", throwIfNotFound: true);

            Move = playerMap.FindAction("Move", true);
            Look = playerMap.FindAction("Look", true);
            Sprint = playerMap.FindAction("Sprint", true);
            Jump = playerMap.FindAction("Jump", true);
            Attack = playerMap.FindAction("Attack", true);
            Heavy = playerMap.FindAction("Heavy", true);
            Block = playerMap.FindAction("Block", true);
            Dodge = playerMap.FindAction("Dodge", true);
            LockOn = playerMap.FindAction("LockOn", true);
            SwitchStyle = playerMap.FindAction("SwitchStyle", true);
            Interact = playerMap.FindAction("Interact", true);
            Pause = playerMap.FindAction("Pause", true);
            Inventory = playerMap.FindAction("Inventory", true);
            UseItem = playerMap.FindAction("UseItem", true);
            CycleSeed = playerMap.FindAction("CycleSeed", true);
            Skill1 = playerMap.FindAction("Skill1", true);
            Skill2 = playerMap.FindAction("Skill2", true);
            Character = playerMap.FindAction("Character", true);
            Journal = playerMap.FindAction("Journal", true);
            Map = playerMap.FindAction("Map", true);
            TrackQuest = playerMap.FindAction("TrackQuest", true);

            Resume = uiMap.FindAction("Resume", true);
            CloseMenu = uiMap.FindAction("CloseMenu", true);
            Navigate = uiMap.FindAction("Navigate", true);
            DialogueAdvance = uiMap.FindAction("Advance", true);

            Skip = cutsceneMap.FindAction("Skip", true);
            Advance = cutsceneMap.FindAction("Advance", true);

            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
            ApplyState(GameState.Boot);
        }

        void OnDestroy()
        {
            EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
            if (actions != null)
            {
                actions.Disable();
                Destroy(actions);
            }
        }

        void OnGameStateChanged(GameStateChangedEvent evt) => ApplyState(evt.Current);

        void ApplyState(GameState state)
        {
            playerMap.Disable();
            uiMap.Disable();
            cutsceneMap.Disable();

            switch (state)
            {
                case GameState.Playing: playerMap.Enable(); break;
                case GameState.MainMenu:
                case GameState.Paused:
                case GameState.InGameMenu: uiMap.Enable(); break;
                case GameState.Cutscene: cutsceneMap.Enable(); break;
            }

            bool lockCursor = state == GameState.Playing;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;
        }
    }
}
