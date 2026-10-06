using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 15 setup (night danger &amp; fatigue): adds PlayerFatigue to the player. Enemies' night behaviour
    /// needs no setup (each EnemyData has Night settings with defaults); it needs the Milestone 14 day/night cycle.
    /// Safe to re-run.
    /// </summary>
    public static class NightDangerSetup
    {
        const string TestWorldScenePath = "Assets/_Project/Scenes/World_Test.unity";

        [MenuItem("Beast/Setup/Run Milestone 15 Setup (Night Danger & Fatigue)", priority = 14)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No object tagged Player in World_Test.");
                return;
            }
            bool added = !player.TryGetComponent(out PlayerFatigue _);
            if (added) player.AddComponent<PlayerFatigue>();
            if (Object.FindFirstObjectByType<DayNightCycle>() == null)
                Debug.LogWarning("[Setup] No day/night cycle in World_Test: run Milestone 14 first, or enemies are never bolder at night.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 15 night danger & fatigue setup complete: fatigue {(added ? "added to" : "already on")} the player. " +
                      "Stay up past midnight to get tired; fight Bandits at night for better loot.");
        }
    }
}
