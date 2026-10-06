using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Beast.Core
{
    /// <summary>Async scene loading that drives the Loading → Playing (or MainMenu) state transition.</summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        GameStateService state;
        GameConfig config;

        public bool IsLoading { get; private set; }
        /// <summary>The world a new game starts in.</summary>
        public string FirstScene => config != null ? config.FirstScene : null;
        public string MainMenuScene => config != null ? config.MainMenuScene : null;
        /// <summary>False until the main menu scene exists and is in Build Settings (Milestone 16 setup).</summary>
        public bool HasMainMenu => !string.IsNullOrEmpty(MainMenuScene) && Application.CanStreamedLevelBeLoaded(MainMenuScene);

        public void Initialize(GameStateService state, GameConfig config)
        {
            this.state = state;
            this.config = config;
        }

        /// <summary>Fire-and-forget version for buttons and boot code.</summary>
        public async void Load(string sceneName, GameState after = GameState.Playing)
        {
            try { await LoadAsync(sceneName, after); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Back to the title screen (unsaved progress is lost).</summary>
        public void LoadMainMenu() => Load(MainMenuScene, GameState.MainMenu);

        public async Awaitable<bool> LoadAsync(string sceneName, GameState after = GameState.Playing)
        {
            if (IsLoading)
            {
                Debug.LogWarning($"[SceneLoader] Already loading; ignored request for '{sceneName}'.");
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneLoader] Scene '{sceneName}' isn't in Build Settings.");
                return false;
            }

            IsLoading = true;
            state.SetState(GameState.Loading);
            try
            {
                await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            finally
            {
                IsLoading = false;
            }

            EventBus<SceneLoadedEvent>.Raise(new SceneLoadedEvent(sceneName));
            state.SetState(after);
            return true;
        }
    }
}
