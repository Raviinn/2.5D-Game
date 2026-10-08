using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 53 setup (tutorial &amp; onboarding): adds the first-day guide to World_Test. The "How to play" page
    /// (Esc → Options) and the Tutorial hints setting need no setup. Safe to re-run.
    /// </summary>
    public static class TutorialSetup
    {
        const string TestWorldScenePath = "Assets/_Project/Scenes/World_Test.unity";
        const string GuideName = "[Tutorial]";

        [MenuItem("Beast/Setup/Run Milestone 53 Setup (Tutorial)", priority = 45)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            bool added = Object.FindFirstObjectByType<TutorialGuide>() == null;
            if (added) new GameObject(GuideName).AddComponent<TutorialGuide>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 53 tutorial setup complete: first-day guide {(added ? "added" : "kept")}. " +
                      "Esc → How to play explains the game; Settings → Interface → Tutorial hints turns the guide off.");
        }
    }
}
