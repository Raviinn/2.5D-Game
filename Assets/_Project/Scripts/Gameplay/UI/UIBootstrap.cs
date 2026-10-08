using UnityEngine;
using UnityEngine.SceneManagement;

namespace Beast.Gameplay
{
    /// <summary>
    /// Adds the HUD, pause menu, quest tracker and minimap to every gameplay scene (any scene with a Player)
    /// that doesn't already have them, so new scenes need no setup step. Components placed in a scene by the
    /// setup menus are kept; nothing is ever added twice.
    /// </summary>
    static class UIBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureUi();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureUi();

        static void EnsureUi()
        {
            if (GameObject.FindWithTag("Player") == null) return;

            GameObject host = null;
            GameObject Host() => host != null ? host : host = new GameObject("[HUD]");

            if (Object.FindFirstObjectByType<GameHud>() == null) Host().AddComponent<GameHud>();
            if (Object.FindFirstObjectByType<PauseMenu>() == null) Host().AddComponent<PauseMenu>();
            if (Object.FindFirstObjectByType<QuestTracker>() == null) Host().AddComponent<QuestTracker>();
            if (Object.FindFirstObjectByType<Minimap>() == null) Host().AddComponent<Minimap>();
            if (Object.FindFirstObjectByType<GameMenu>() == null) Host().AddComponent<GameMenu>();
            if (Object.FindFirstObjectByType<CraftingScreen>() == null) Host().AddComponent<CraftingScreen>();
            if (Object.FindFirstObjectByType<StorageScreen>() == null) Host().AddComponent<StorageScreen>();
            if (Object.FindFirstObjectByType<MirrorScreen>() == null) Host().AddComponent<MirrorScreen>();
        }
    }
}
