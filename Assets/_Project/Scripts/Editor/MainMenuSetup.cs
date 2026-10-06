using System.IO;
using System.Linq;
using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 16 setup (main menu &amp; save slots): creates the MainMenu scene (a camera and the title screen),
    /// puts it in Build Settings right after Bootstrap (removing Unity's template SampleScene) and points GameConfig at it. Save slots need no setup.
    /// Safe to re-run.
    /// </summary>
    public static class MainMenuSetup
    {
        const string Root = "Assets/_Project";
        const string BootstrapScenePath = Root + "/Scenes/Bootstrap.unity";
        const string MainMenuScenePath = Root + "/Scenes/MainMenu.unity";
        const string ConfigPath = Root + "/Resources/GameConfig.asset";
        const string MainMenuSceneName = "MainMenu";
        const string TemplateScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Beast/Setup/Run Milestone 16 Setup (Main Menu & Save Slots)", priority = 15)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ConfigPath) || !File.Exists(BootstrapScenePath))
            {
                Debug.LogError("[Setup] GameConfig or the Bootstrap scene is missing. Run the earlier setups first.");
                return;
            }

            bool created = !File.Exists(MainMenuScenePath);
            var scene = created
                ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)
                : EditorSceneManager.OpenScene(MainMenuScenePath);

            if (Object.FindFirstObjectByType<Camera>() == null)
            {
                // The menu is drawn over everything; the camera only keeps the screen from saying "No cameras rendering".
                var camera = new GameObject("Menu Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.05f, 0.1f);
                camera.cullingMask = 0;
            }
            if (Object.FindFirstObjectByType<MainMenu>() == null) new GameObject("[Main Menu]").AddComponent<MainMenu>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);

            // Build order: Bootstrap, MainMenu, then everything else (the world scenes) as it was.
            // Unity's template scene (Assets/Scenes/SampleScene) is dropped so it never ships in a build.
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.path != BootstrapScenePath && s.path != MainMenuScenePath && s.path != TemplateScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(MainMenuScenePath, true));
            scenes.Insert(0, new EditorBuildSettingsScene(BootstrapScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            // Loaded only now: opening or creating a scene unloads assets nothing references.
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var serialized = new SerializedObject(config);
            serialized.FindProperty(nameof(GameConfig.MainMenuScene)).stringValue = MainMenuSceneName;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();

            Debug.Log($"[Setup] Milestone 16 main menu & save slots setup complete: MainMenu scene {(created ? "created" : "updated")} and added to Build Settings. " +
                      "Press Play in the Bootstrap or MainMenu scene for the title screen (Play in World_Test still skips it, playing in slot 1).");
        }
    }
}
