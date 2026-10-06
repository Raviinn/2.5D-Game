using UnityEngine;
using UnityEngine.SceneManagement;

namespace Beast.Core
{
    /// <summary>
    /// Single entry point. Creates the persistent [Services] object before any scene loads,
    /// so pressing Play in ANY scene works. If the game starts in the Bootstrap scene, it continues to the
    /// main menu (or straight to GameConfig.FirstScene when there's no main menu scene yet). Pressing Play in
    /// a world scene skips the menu and plays in save slot 1.
    /// </summary>
    public static class Bootstrapper
    {
        static GameConfig config;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => config = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateServices()
        {
            config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            if (config == null)
            {
                Debug.LogError("[Bootstrap] No GameConfig found in Resources. Run Beast > Setup > Run Milestone 1 Setup.");
                return;
            }
            if (config.InputActions == null)
            {
                Debug.LogError("[Bootstrap] GameConfig has no Input Actions assigned.", config);
                config = null;
                return;
            }

            var root = new GameObject("[Services]");
            Object.DontDestroyOnLoad(root);

            // Order matters: each service only depends on the ones created before it.
            var input = Add<InputService>(root);
            input.Initialize(config.InputActions);

            Add<SettingsService>(root).Initialize();

            var state = Add<GameStateService>(root);
            state.Initialize(input);

            Add<HitStop>(root).Initialize(state);

            var sceneLoader = Add<SceneLoader>(root);
            sceneLoader.Initialize(state, config);

            var save = Add<SaveService>(root);
            save.Initialize(state, sceneLoader);

            var clock = Add<WorldClock>(root);
            clock.Initialize(config, state, save);

            if (config.Database != null)
            {
                config.Database.Initialize();
                Services.Register(config.Database);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            root.AddComponent<DebugOverlay>().Initialize(state, clock, save);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnterFirstScene()
        {
            if (config == null) return;

            var loader = Services.Get<SceneLoader>();
            string active = SceneManager.GetActiveScene().name;
            if (active == config.BootstrapScene)
            {
                if (loader.HasMainMenu) loader.LoadMainMenu();
                else loader.Load(config.FirstScene);
            }
            else if (active == config.MainMenuScene)
                Services.Get<GameStateService>().SetState(GameState.MainMenu);
            else
                Services.Get<GameStateService>().SetState(GameState.Playing);
        }

        static T Add<T>(GameObject root) where T : Component
        {
            var service = root.AddComponent<T>();
            Services.Register(service);
            return service;
        }
    }
}
