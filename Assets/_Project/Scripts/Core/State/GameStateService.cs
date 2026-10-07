using UnityEngine;
using UnityEngine.InputSystem;

namespace Beast.Core
{
    public enum GameState
    {
        Boot,
        MainMenu,
        Loading,
        Playing,
        Paused,
        Cutscene,
        /// <summary>An in-game menu (inventory, map...) is open. Time is frozen and the UI has input.</summary>
        InGameMenu,
    }

    /// <summary>Owns the current game mode. Other systems react via GameStateChangedEvent.</summary>
    public sealed class GameStateService : MonoBehaviour
    {
        public GameState Current { get; private set; } = GameState.Boot;

        /// <summary>
        /// While true, the menu hotkeys (Tab / I / C / J) don't close the current menu: set during conversations,
        /// so a stray key press never abandons dialogue. Esc still leaves.
        /// </summary>
        public bool BlockHotkeyClose { get; set; }

        /// <summary>
        /// While true, the Resume key (Esc) doesn't unpause or close the menu: set while a sub-view inside the game menu
        /// (a settings category, a confirm) is open, so Esc closes that instead. Reset by any state change.
        /// </summary>
        public bool HoldPause { get; set; }

        InputService input;

        public void Initialize(InputService input)
        {
            this.input = input;
            input.Pause.performed += OnPausePressed;
            input.Resume.performed += OnResumePressed;
            input.CloseMenu.performed += OnCloseMenuPressed;
        }

        void OnDestroy()
        {
            if (input == null) return;
            input.Pause.performed -= OnPausePressed;
            input.Resume.performed -= OnResumePressed;
            input.CloseMenu.performed -= OnCloseMenuPressed;
        }

        public void SetState(GameState next)
        {
            if (next == Current) return;

            var previous = Current;
            Current = next;
            if (next != GameState.InGameMenu) BlockHotkeyClose = false; // never outlives the menu that set it
            HoldPause = false;
            Time.timeScale = next is GameState.Paused or GameState.InGameMenu ? 0f : 1f;
            EventBus<GameStateChangedEvent>.Raise(new GameStateChangedEvent(previous, next));
        }

        void OnPausePressed(InputAction.CallbackContext _)
        {
            if (Current == GameState.Playing) SetState(GameState.Paused);
        }

        void OnResumePressed(InputAction.CallbackContext _)
        {
            if (HoldPause && Current is GameState.Paused or GameState.InGameMenu) return;
            if (Current is GameState.Paused or GameState.InGameMenu) SetState(GameState.Playing);
        }

        void OnCloseMenuPressed(InputAction.CallbackContext _)
        {
            if (Current == GameState.InGameMenu && !BlockHotkeyClose) SetState(GameState.Playing);
        }
    }
}
