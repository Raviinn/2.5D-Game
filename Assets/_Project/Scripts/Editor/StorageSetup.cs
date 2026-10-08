using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 27 setup (storage): a 60-slot storage chest beside the homestead bed, linked to the workbench so
    /// crafting can use what's inside. Safe to re-run: an existing chest (and its contents in saves) is kept.
    /// Milestone 37: the chest is never placed on the farm field; a chest already standing on it is moved off.
    /// </summary>
    public static class StorageSetup
    {
        const string TestWorldScenePath = "Assets/_Project/Scenes/World_Test.unity";
        const string ChestName = "Storage_Chest";
        public const int ChestSlots = 60;

        [MenuItem("Beast/Setup/Run Milestone 37 Setup (Chest Off the Field)", priority = 33)]
        public static void RunMilestone37() => Run();

        [MenuItem("Beast/Setup/Run Milestone 27 Setup (Storage Chest)", priority = 23)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            var materialPaths = EnvironmentSetup.EnsureMaterials(EnvironmentArtGenerator.EnsureTextures());
            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            var wood = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Wood"]);
            var stone = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Stone"]);

            string report;
            var chest = GameObject.Find(ChestName);
            var homeBed = Object.FindFirstObjectByType<SleepSpot>();
            if (chest != null && (OnField(chest.transform.position) || OffTheFloor(chest.transform, homeBed) || FarFromBed(chest.transform, homeBed)))
            {
                var bed = homeBed;
                // Out of the way while searching, so the chest's own collider doesn't block its new spot.
                chest.SetActive(false);
                var spot = bed != null ? FindSpot(bed.transform) : chest.transform.position;
                chest.SetActive(true);
                chest.transform.position = spot;
                if (bed != null) FaceAwayFrom(chest.transform, bed.transform);
                report = $"chest moved to the floor beside the bed, off the farm field, at {spot:F1}";
            }
            else if (chest != null)
            {
                report = "chest kept";
            }
            else
            {
                var bed = Object.FindFirstObjectByType<SleepSpot>();
                if (bed == null)
                {
                    Debug.LogError("[Setup] No bed (Sleep Spot) in World_Test. Run the Milestone 5 setup first.");
                    return;
                }
                chest = BuildChest(FindSpot(bed.transform), bed.transform, wood, stone);
                report = $"chest placed beside the bed at {chest.transform.position:F1}";
            }

            var inventory = chest.GetComponent<Inventory>() != null ? chest.GetComponent<Inventory>() : chest.AddComponent<Inventory>();
            var so = new SerializedObject(inventory);
            so.FindProperty("slotCount").intValue = ChestSlots;
            so.FindProperty("saveId").stringValue = "storage.homestead";
            so.FindProperty("announceAdds").boolValue = false;
            so.FindProperty("describeInSave").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (!chest.TryGetComponent(out StorageChest storage)) storage = chest.AddComponent<StorageChest>();

            var bench = Object.FindFirstObjectByType<Workbench>();
            if (bench != null)
            {
                var benchSo = new SerializedObject(bench);
                benchSo.FindProperty("storage").objectReferenceValue = storage;
                benchSo.ApplyModifiedPropertiesWithoutUndo();
                report += "; the workbench can use its contents";
            }
            else
            {
                report += "; no workbench yet (run Milestone 26 to link one)";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 27 storage setup complete: {report}. {ChestSlots} slots.");
        }

        /// <summary>A clear, level spot near the bed, away from the workbench (tries each side, then further out).</summary>
        static Vector3 FindSpot(Transform bed)
        {
            Physics.SyncTransforms();
            // The floor the bed stands on: a spot must be on the same floor, never on top of the workbench or a crate.
            float floor = bed.TryGetComponent(out Collider bedCollider) ? bedCollider.bounds.min.y : bed.position.y;
            var directions = new[] { -bed.right, bed.right, -bed.forward, bed.forward };
            foreach (float distance in new[] { 2f, 2.8f, 3.6f, 4.5f })
                foreach (var direction in directions)
                {
                    var flat = new Vector3(direction.x, 0f, direction.z).normalized;
                    var spot = bed.position + flat * distance;
                    var from = new Vector3(spot.x, bed.position.y + 1.5f, spot.z);
                    if (!Physics.Raycast(from, Vector3.down, out var ground, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(ground.point.y - floor) > 0.3f || OnField(ground.point)) continue;
                    if (Physics.CheckBox(ground.point + Vector3.up * 0.45f, new Vector3(0.75f, 0.4f, 0.6f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return ground.point;
                }
            return bed.position + bed.forward * 2.2f;
        }

        /// <summary>True when a chest at this point would stand on the farm field or its fence (with room to walk).</summary>
        public static bool OnField(Vector3 point)
        {
            var plot = Object.FindFirstObjectByType<FarmPlot>();
            if (plot == null) return false;
            var so = new SerializedObject(plot);
            float tile = so.FindProperty("tileSize").floatValue;
            var min = plot.transform.position;
            var max = min + new Vector3(so.FindProperty("width").intValue * tile, 0f, so.FindProperty("depth").intValue * tile);
            const float margin = 1.3f; // fence (0.6 m out) + half the chest + a step
            return point.x > min.x - margin && point.x < max.x + margin && point.z > min.z - margin && point.z < max.z + margin;
        }

        /// <summary>True when the chest isn't standing on the bed's floor (e.g. on top of the workbench).</summary>
        static bool OffTheFloor(Transform chest, SleepSpot bed)
        {
            if (bed == null) return false;
            float floor = bed.TryGetComponent(out Collider bedCollider) ? bedCollider.bounds.min.y : bed.transform.position.y;
            return Mathf.Abs(chest.position.y - floor) > 0.3f;
        }

        static bool FarFromBed(Transform chest, SleepSpot bed)
        {
            if (bed == null) return false;
            var offset = chest.position - bed.transform.position;
            offset.y = 0f;
            return offset.magnitude > 3.5f;
        }

        static void FaceAwayFrom(Transform chest, Transform bed)
        {
            var toBed = bed.position - chest.position;
            toBed.y = 0f;
            if (toBed.sqrMagnitude > 0.01f) chest.rotation = Quaternion.LookRotation(-toBed); // lid hinge away from the bed
        }

        static GameObject BuildChest(Vector3 groundPoint, Transform bed, Material wood, Material iron)
        {
            var chest = new GameObject(ChestName);
            chest.transform.position = groundPoint;
            var toBed = bed.position - groundPoint;
            toBed.y = 0f;
            if (toBed.sqrMagnitude > 0.01f) chest.transform.rotation = Quaternion.LookRotation(-toBed); // lid hinge away from the bed
            chest.isStatic = true;
            var collider = chest.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.35f, 0f);
            collider.size = new Vector3(1.1f, 0.7f, 0.65f);

            Part("Body", chest.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 0.65f), wood);
            Part("Lid", chest.transform, new Vector3(0f, 0.65f, 0f), new Vector3(1.14f, 0.12f, 0.69f), wood);
            foreach (var x in new[] { -0.4f, 0.4f })
                Part("Band", chest.transform, new Vector3(x, 0.36f, 0f), new Vector3(0.08f, 0.74f, 0.71f), iron);
            Part("Lock", chest.transform, new Vector3(0f, 0.55f, 0.35f), new Vector3(0.14f, 0.16f, 0.04f), iron);
            return chest;
        }

        static void Part(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            go.isStatic = true;
        }
    }
}
