using System.Collections.Generic;
using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 11 setup (climbing &amp; ledge grab): adds hang/climb frames to the Knight's placeholder sprites,
    /// the PlayerClimber to the player, marks the greybox houses as not climbable (their roofs have no collider),
    /// and builds a small practice course west of the village:
    ///   a 1.4 m block (vault) · a 2.3 m block (vault) · a 3.2 m ledge wall (hang, shimmy along 9 m, pull up)
    ///   · a 6 m ivy cliff behind it (climb from the wall's top or from the ground, pull up at the top).
    /// Safe to re-run: the course is rebuilt; environment dressing that overlaps it is removed.
    /// </summary>
    public static class ClimbingSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string CourseName = "Climbing_Course";
        const string DressingName = "Environment_Dressing";

        /// <summary>Course origin: open ground west of the village, inside the forest ring.</summary>
        static readonly Vector3 Origin = new(-20f, 0f, -13f);

        [MenuItem("Beast/Setup/Run Milestone 11 Setup (Climbing)", priority = 10)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }

            string spriteReport = SpriteVisualsSetup.UpgradeSheet("Knight", SpriteVisualsSetup.KnightPalette);
            var materialPaths = EnvironmentSetup.EnsureMaterials(EnvironmentArtGenerator.EnsureTextures());
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(materialPaths[name]);

            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No object tagged Player in World_Test.");
                return;
            }
            if (!player.TryGetComponent(out PlayerClimber _)) player.AddComponent<PlayerClimber>();

            int houses = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("House_") && t.GetComponent<Collider>() != null && !t.TryGetComponent(out NotClimbable _))
                {
                    t.gameObject.AddComponent<NotClimbable>();
                    houses++;
                }

            var old = GameObject.Find(CourseName);
            if (old != null) Object.DestroyImmediate(old);
            var course = new GameObject(CourseName).transform;
            course.position = Origin;

            var stone = Mat("Env_Stone");
            var ivy = Mat("Env_Ivy");
            var wood = Mat("Env_Wood");

            // Blocks you vault: walk up, jump toward them.
            Block("Vault_Low", course, new Vector3(5f, 0.7f, 3.5f), new Vector3(2f, 1.4f, 2f), stone);
            Lip(course, new Vector3(5f, 1.4f, 3.5f), new Vector3(2f, 0f, 2f), Vector3.right, wood);
            Block("Vault_High", course, new Vector3(5f, 1.15f, -0.5f), new Vector3(2f, 2.3f, 2f), stone);
            Lip(course, new Vector3(5f, 2.3f, -0.5f), new Vector3(2f, 0f, 2f), Vector3.right, wood);

            // Ledge wall: too tall to vault, so you hang from it, shimmy along and pull up.
            Block("Ledge_Wall", course, new Vector3(1f, 1.6f, 0f), new Vector3(2f, 3.2f, 9f), stone);
            Lip(course, new Vector3(1f, 3.2f, 0f), new Vector3(2f, 0f, 9f), Vector3.right, wood);

            // Ivy cliff: every side is climbable.
            var cliff = Block("Ivy_Cliff", course, new Vector3(-2.5f, 3f, 0f), new Vector3(5f, 6f, 9f), ivy);
            cliff.AddComponent<ClimbableSurface>();

            int cleared = ClearDressing(course);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 11 climbing setup complete. {spriteReport}. {houses} house(s) marked not climbable, " +
                      $"{cleared} overlapping dressing object(s) removed. The practice course is west of the village (M: map).");
        }

        static GameObject Block(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            string key = $"Box_Course_{size.x:0.##}x{size.y:0.##}x{size.z:0.##}";
            go.GetComponent<MeshFilter>().sharedMesh = EnvironmentArtGenerator.SaveMesh(key, m => EnvironmentArtGenerator.BuildWorldUvBox(m, size, 1.5f));
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.isStatic = true;
            return go;
        }

        /// <summary>A wooden lip along a block's top front edge: reads as "grab here". Visual only.</summary>
        static void Lip(Transform parent, Vector3 topCenter, Vector3 blockSize, Vector3 front, Material wood)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Ledge_Lip";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            float depth = Vector3.Dot(blockSize, Abs(front)) * 0.5f;
            float length = Vector3.Dot(blockSize, Abs(Vector3.Cross(Vector3.up, front)));
            go.transform.localPosition = topCenter + front * (depth - 0.1f) + Vector3.down * 0.06f;
            go.transform.localRotation = Quaternion.LookRotation(front);
            go.transform.localScale = new Vector3(length + 0.04f, 0.14f, 0.24f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = wood;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Removes trees, rocks, bushes and grass from the Milestone 9 dressing that overlap the course.</summary>
        static int ClearDressing(Transform course)
        {
            var dressing = GameObject.Find(DressingName);
            if (dressing == null) return 0;

            var bounds = new Bounds(course.position, Vector3.zero);
            foreach (var r in course.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            bounds.Expand(new Vector3(3f, 100f, 3f));

            var doomed = new List<GameObject>();
            foreach (Transform group in dressing.transform)
                foreach (Transform item in group)
                    if (bounds.Contains(item.position)) doomed.Add(item.gameObject);
            foreach (var go in doomed) Object.DestroyImmediate(go);
            return doomed.Count;
        }

        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
