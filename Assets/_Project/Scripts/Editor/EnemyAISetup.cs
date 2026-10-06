using System.Collections.Generic;
using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 13 setup (smarter enemies): adds a third Bandit to the camp so turn-taking shows in fights.
    /// Pathfinding needs no setup (the navigation mesh is built when the scene loads) and the new tactics
    /// settings live on each EnemyData asset (Hold Distance, Give Up After, Leash Range). Safe to re-run.
    /// </summary>
    public static class EnemyAISetup
    {
        const string TestWorldScenePath = "Assets/_Project/Scenes/World_Test.unity";
        const string NewBanditName = "Bandit_C";
        static readonly Vector3 NewBanditPosition = new(21f, 1.1f, 17f);

        [MenuItem("Beast/Setup/Run Milestone 13 Setup (Smarter Enemies)", priority = 12)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            string report;
            if (GameObject.Find(NewBanditName) != null)
            {
                report = $"{NewBanditName} already exists; kept as-is";
            }
            else
            {
                var template = GameObject.Find("Bandit_A");
                if (template == null)
                {
                    Debug.LogError("[Setup] Bandit_A not found in World_Test. Run the Milestone 2 setup first.");
                    return;
                }
                var bandit = Object.Instantiate(template, template.transform.parent);
                bandit.name = NewBanditName;
                bandit.transform.SetPositionAndRotation(NewBanditPosition, Quaternion.Euler(0f, 225f, 0f));
                int cleared = ClearDressingAround(NewBanditPosition, 2.5f);
                report = $"{NewBanditName} added to the bandit camp ({cleared} overlapping dressing object(s) removed)";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 13 smarter-enemies setup complete: {report}. Enemies now path around obstacles, " +
                      $"take turns ({EnemyDirector.MaxAttackers} attack at once), give up when you climb out of reach, and walk home to heal.");
        }

        static int ClearDressingAround(Vector3 center, float radius)
        {
            var dressing = GameObject.Find("Environment_Dressing");
            if (dressing == null) return 0;
            var doomed = new List<GameObject>();
            foreach (Transform group in dressing.transform)
                foreach (Transform item in group)
                {
                    var offset = item.position - center;
                    offset.y = 0f;
                    if (offset.magnitude < radius && item.GetComponentInChildren<Collider>() != null) doomed.Add(item.gameObject);
                }
            foreach (var go in doomed) Object.DestroyImmediate(go);
            return doomed.Count;
        }
    }
}
