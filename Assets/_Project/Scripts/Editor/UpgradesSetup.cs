using System.IO;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 49 setup (homestead upgrades): a signpost by the bed with the homestead plans, four improvements
    /// (Kitchen Garden, Larger Chest, Copper Still, Hay Loft), and the kitchen garden itself: a fenced 4×4 field,
    /// hidden until built. Safe to re-run: the plans and the garden are kept (costs are re-applied to the plans).
    /// </summary>
    public static class UpgradesSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        public const string PlansName = "Homestead_Plans";
        public const string GardenName = "Kitchen_Garden";
        const int GardenSize = 4;
        static readonly Vector3[] GardenCandidates = { new(16f, 0f, 3f), new(17f, 0f, -14f), new(-4f, 0f, -16f), new(14f, 0f, 10f) };

        [MenuItem("Beast/Setup/Run Milestone 49 Setup (Homestead Upgrades)", priority = 41)]
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
            ItemData Item(string name) => AssetDatabase.LoadAssetAtPath<ItemData>($"{Root}/Data/Items/{name}.asset");
            var scrap = Item("Item_IronScrap");
            var cloth = Item("Item_BanditCloth");
            var wood = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Wood"]);
            var dirt = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Dirt"]);
            var notice = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Notice"]);
            var bed = Object.FindFirstObjectByType<SleepSpot>();
            var field = Object.FindFirstObjectByType<FarmPlot>();
            var chest = Object.FindFirstObjectByType<StorageChest>();
            if (scrap == null || cloth == null || bed == null || field == null)
            {
                Debug.LogError("[Setup] Needs the bed, the farm field, Iron Scrap and Bandit Cloth. Run the earlier setups first.");
                return;
            }
            Physics.SyncTransforms();

            // ---- The kitchen garden (hidden until built) ----
            var garden = FindIncludingInactive(GardenName);
            string gardenReport = "garden kept";
            if (garden == null)
            {
                var corner = ChooseGarden();
                garden = new GameObject(GardenName);
                garden.transform.position = corner;
                var plotObject = new GameObject("Field");
                plotObject.transform.SetParent(garden.transform, false);
                var plot = plotObject.AddComponent<FarmPlot>();
                EditorUtility.CopySerialized(field, plot); // crop material, soil texture, reach...
                var pso = new SerializedObject(plot);
                pso.FindProperty("width").intValue = GardenSize;
                pso.FindProperty("depth").intValue = GardenSize;
                pso.FindProperty("saveId").stringValue = "farm.garden";
                pso.ApplyModifiedPropertiesWithoutUndo();
                var groundPatch = Block("GardenBase", garden.transform, new Vector3(GardenSize * 0.5f, 0.005f, GardenSize * 0.5f),
                    new Vector3(GardenSize + 0.4f, 0.01f, GardenSize + 0.4f), dirt, false);
                BuildFence(garden.transform, wood);
                garden.SetActive(false);
                gardenReport = $"kitchen garden at {corner:F0} (hidden until built)";
            }

            // ---- The plans signpost ----
            var plansObject = GameObject.Find(PlansName);
            if (plansObject == null)
            {
                plansObject = new GameObject(PlansName);
                plansObject.transform.position = SignSpot(bed.transform);
                var toBed = bed.transform.position - plansObject.transform.position;
                toBed.y = 0f;
                if (toBed.sqrMagnitude > 0.01f) plansObject.transform.rotation = Quaternion.LookRotation(toBed);
                Block("Post", plansObject.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.12f, 1.2f, 0.12f), wood, true);
                Block("Board", plansObject.transform, new Vector3(0f, 1.2f, 0.05f), new Vector3(0.8f, 0.55f, 0.06f), wood, false);
                Block("Sheet", plansObject.transform, new Vector3(0f, 1.2f, 0.09f), new Vector3(0.6f, 0.4f, 0.01f), notice != null ? notice : wood, false);
                plansObject.AddComponent<UpgradesScreen>();
            }
            var plans = plansObject.GetComponent<HomesteadUpgrades>() ?? plansObject.AddComponent<HomesteadUpgrades>();
            var so = new SerializedObject(plans);
            so.FindProperty("kitchenGarden").objectReferenceValue = garden;
            so.FindProperty("chest").objectReferenceValue = chest;
            var list = so.FindProperty("upgrades");
            list.arraySize = 4;
            SetUpgrade(list.GetArrayElementAtIndex(0), HomesteadUpgradeKind.KitchenGarden, "Kitchen Garden",
                "Fence off a second, smaller field (4 by 4) beside the homestead. More room for seeds.", 250, (scrap, 6), (cloth, 4));
            SetUpgrade(list.GetArrayElementAtIndex(1), HomesteadUpgradeKind.LargerChest, "Larger Chest",
                "Iron bands and a deeper box: the storage chest holds 100 stacks instead of 60.", 150, (scrap, 4));
            SetUpgrade(list.GetArrayElementAtIndex(2), HomesteadUpgradeKind.CopperStill, "Copper Still",
                "A proper still and stockpot at the workbench: alchemy and cooking make one extra serving.", 200, (scrap, 8));
            SetUpgrade(list.GetArrayElementAtIndex(3), HomesteadUpgradeKind.HayLoft, "Hay Loft",
                "Store feed above the pen: one fill of the trough feeds the animals for three days.", 120, (scrap, 2), (cloth, 3));
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 49 homestead upgrades setup complete: plans by the bed; {gardenReport}. " +
                      "Build the Kitchen Garden, Larger Chest, Copper Still and Hay Loft with gold and materials.");
        }

        static void SetUpgrade(SerializedProperty element, HomesteadUpgradeKind kind, string title, string description, int gold, params (ItemData item, int count)[] costs)
        {
            element.FindPropertyRelative("Kind").enumValueIndex = (int)kind;
            element.FindPropertyRelative("Title").stringValue = title;
            element.FindPropertyRelative("Description").stringValue = description;
            element.FindPropertyRelative("Gold").intValue = gold;
            var materials = element.FindPropertyRelative("Materials");
            materials.arraySize = costs.Length;
            for (int i = 0; i < costs.Length; i++)
            {
                materials.GetArrayElementAtIndex(i).FindPropertyRelative("Item").objectReferenceValue = costs[i].item;
                materials.GetArrayElementAtIndex(i).FindPropertyRelative("Count").intValue = costs[i].count;
            }
        }

        static GameObject FindIncludingInactive(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name && t.parent == null) return t.gameObject;
            return null;
        }

        /// <summary>The garden's corner (it spans +x and +z from here) on open ground.</summary>
        static Vector3 ChooseGarden()
        {
            foreach (var c in GardenCandidates)
            {
                var centre = c + new Vector3(GardenSize * 0.5f, 1.2f, GardenSize * 0.5f);
                if (Physics.OverlapBox(centre, new Vector3(GardenSize * 0.5f + 1f, 1f, GardenSize * 0.5f + 1f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Length == 0)
                    return c;
            }
            return GardenCandidates[0];
        }

        static Vector3 SignSpot(Transform bed)
        {
            foreach (var offset in new[] { new Vector3(-1.4f, 0f, 2f), new Vector3(1.6f, 0f, 2f), new Vector3(1.8f, 0f, -2.4f), new Vector3(0f, 0f, 3f) })
            {
                var p = new Vector3(bed.position.x + offset.x, 0f, bed.position.z + offset.z);
                if (!Physics.CheckBox(p + Vector3.up * 0.8f, new Vector3(0.5f, 0.6f, 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return p;
            }
            return new Vector3(bed.position.x, 0f, bed.position.z + 3f);
        }

        static void BuildFence(Transform garden, Material wood)
        {
            const float margin = 0.5f;
            var a = new Vector3(-margin, 0f, -margin);
            var corners = new[] { a, new Vector3(GardenSize + margin, 0f, -margin), new Vector3(GardenSize + margin, 0f, GardenSize + margin), new Vector3(-margin, 0f, GardenSize + margin) };
            var fence = new GameObject("Fence").transform;
            fence.SetParent(garden, false);
            Vector3 gate = new(GardenSize * 0.5f, 0f, -margin); // the gate faces south
            for (int side = 0; side < 4; side++)
            {
                var from = corners[side];
                var to = corners[(side + 1) % 4];
                int spans = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(from, to) / 1.5f));
                for (int i = 0; i < spans; i++)
                {
                    var post = Vector3.Lerp(from, to, i / (float)spans);
                    var next = Vector3.Lerp(from, to, (i + 1) / (float)spans);
                    if ((post - gate).magnitude >= 1f) Block("Post", fence, post + Vector3.up * 0.5f, new Vector3(0.14f, 1f, 0.14f), wood, true);
                    var mid = (post + next) * 0.5f;
                    if ((mid - gate).magnitude < 1.1f) continue;
                    foreach (float h in new[] { 0.42f, 0.78f })
                    {
                        var rail = Block("Rail", fence, mid + Vector3.up * h, new Vector3(0.06f, 0.08f, Vector3.Distance(post, next)), wood, true);
                        rail.localRotation = Quaternion.LookRotation(next - post);
                    }
                }
            }
        }

        static Transform Block(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            return go.transform;
        }
    }
}
