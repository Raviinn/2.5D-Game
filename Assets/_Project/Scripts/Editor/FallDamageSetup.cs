using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 24 setup (fall damage &amp; climbing down): adds PlayerFallDamage to the player and a 10 m ivy tower
    /// to the climbing course, south of the ivy cliff. Climb it, then jump off (it hurts) or climb back down (it doesn't).
    /// Climbing down needs no setup: stand still at any top edge, facing the drop, and press Space.
    /// Safe to re-run: the tower is rebuilt.
    /// </summary>
    public static class FallDamageSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string CourseName = "Climbing_Course";
        const string TowerName = "Ivy_Tower";

        /// <summary>Tower centre and size, relative to the course origin (ClimbingSetup).</summary>
        public static readonly Vector3 TowerCenter = new(-2.5f, 5f, -8f);
        public static readonly Vector3 TowerSize = new(3f, 10f, 3f);

        [MenuItem("Beast/Setup/Run Milestone 24 Setup (Fall Damage & Climbing Down)", priority = 20)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }

            var materialPaths = EnvironmentSetup.EnsureMaterials(EnvironmentArtGenerator.EnsureTextures());
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);

            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No object tagged Player in World_Test.");
                return;
            }
            if (!player.TryGetComponent(out PlayerFallDamage _)) player.AddComponent<PlayerFallDamage>();

            var course = GameObject.Find(CourseName);
            if (course == null)
            {
                Debug.LogError("[Setup] No climbing course. Run the Milestone 11 setup first.");
                return;
            }
            var old = course.transform.Find(TowerName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = TowerName;
            tower.transform.SetParent(course.transform, false);
            tower.transform.localPosition = TowerCenter;
            tower.transform.localScale = TowerSize;
            string key = $"Box_Course_{TowerSize.x:0.##}x{TowerSize.y:0.##}x{TowerSize.z:0.##}";
            tower.GetComponent<MeshFilter>().sharedMesh = EnvironmentArtGenerator.SaveMesh(key, m => EnvironmentArtGenerator.BuildWorldUvBox(m, TowerSize, 1.5f));
            tower.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Ivy"]);
            tower.isStatic = true;
            tower.AddComponent<ClimbableSurface>();

            int cleared = ClearOverlaps(tower);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 24 setup complete. Fall damage on the player; a 10 m ivy tower on the climbing course " +
                      $"({cleared} overlapping dressing object(s) removed).");
        }

        /// <summary>Removes environment dressing (trees, rocks, grass) standing in or right next to the tower.</summary>
        static int ClearOverlaps(GameObject tower)
        {
            var dressing = GameObject.Find("Environment_Dressing");
            if (dressing == null) return 0;
            var bounds = tower.GetComponent<Renderer>().bounds;
            bounds.Expand(new Vector3(3f, 100f, 3f));
            var doomed = new System.Collections.Generic.List<GameObject>();
            foreach (Transform group in dressing.transform)
                foreach (Transform item in group)
                    if (bounds.Contains(item.position)) doomed.Add(item.gameObject);
            foreach (var go in doomed) Object.DestroyImmediate(go);
            return doomed.Count;
        }
    }
}
