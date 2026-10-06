using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 6 setup: a general store (ShopData), a merchant NPC with a placeholder sprite near the homestead,
    /// and the trade screen in World_Test. Safe to re-run; existing assets keep your tuning.
    /// </summary>
    public static class EconomySetup
    {
        const string Root = "Assets/_Project";
        const string ItemsFolder = Root + "/Data/Items";
        const string EconomyFolder = Root + "/Data/Economy";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string SpriteMaterialPath = Root + "/Art/Materials/Sprite_Lit.mat";
        const string NpcLayer = "NPC";

        [MenuItem("Beast/Setup/Run Milestone 6 Setup (Economy)", priority = 5)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var turnipSeeds = LoadItem("Item_TurnipSeeds");
            var healrootSeeds = LoadItem("Item_HealrootSeeds");
            var bread = LoadItem("Item_Bread");
            var draught = LoadItem("Item_HealingDraught");
            if (turnipSeeds == null || healrootSeeds == null || bread == null || draught == null ||
                !System.IO.File.Exists(TestWorldScenePath) || AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath) == null)
            {
                Debug.LogError("[Setup] Run the Milestone 1–5 setups first (items, sprites and the World_Test scene are required).");
                return;
            }

            int npcLayer = CombatPrototypeSetup.EnsureLayer(NpcLayer);
            if (npcLayer < 0) return;

            EnsureFolder(EconomyFolder);
            var shop = AssetDatabase.LoadAssetAtPath<ShopData>($"{EconomyFolder}/Shop_GeneralStore.asset");
            if (shop == null)
            {
                shop = ScriptableObject.CreateInstance<ShopData>();
                shop.MerchantName = "Oswin's Provisions";
                shop.Greeting = "Seeds, bread, a draught or two. And I'll buy whatever you drag back from the wilds — at a fair price, mostly.";
                shop.Stock = new[]
                {
                    Stock(turnipSeeds, 20),
                    Stock(healrootSeeds, 5),
                    Stock(bread, 10),
                    Stock(draught, 3),
                };
                AssetDatabase.CreateAsset(shop, $"{EconomyFolder}/Shop_GeneralStore.asset");
            }

            string sheetPath = SpriteVisualsSetup.CreateSheet("Merchant", new PlaceholderSpriteGenerator.Palette
            {
                Body = new Color32(62, 112, 72, 255), Trim = new Color32(190, 160, 110, 255), Skin = new Color32(226, 180, 140, 255),
                Hair = new Color32(120, 80, 45, 255), Outline = new Color32(22, 26, 20, 255),
            });
            string shopPath = AssetDatabase.GetAssetPath(shop);

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            shop = AssetDatabase.LoadAssetAtPath<ShopData>(shopPath);
            var sheet = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(sheetPath);
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);

            CreateMerchant(shop, sheet, spriteMaterial, npcLayer);
            ExcludeLayerFromCamera(npcLayer);
            if (Object.FindFirstObjectByType<ShopScreen>() == null)
                new GameObject("ShopUI").AddComponent<ShopScreen>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Setup] Milestone 6 economy setup complete. The merchant stands north of the field, facing the spawn.");
        }

        static void CreateMerchant(ShopData shop, DirectionalSpriteSheet sheet, Material material, int layer)
        {
            if (GameObject.Find("Merchant") != null)
            {
                Debug.Log("[Setup] Merchant already exists in World_Test; kept as-is.");
                return;
            }

            // Capsule collider stays so the player can't walk through; the mesh is replaced by the sprite visual.
            var merchant = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            merchant.name = "Merchant";
            merchant.transform.SetPositionAndRotation(new Vector3(6f, 1f, 2.5f), Quaternion.Euler(0f, 200f, 0f));
            merchant.layer = layer;

            merchant.AddComponent<NpcController>();
            var shopkeeper = merchant.AddComponent<Shopkeeper>();
            var so = new SerializedObject(shopkeeper);
            so.FindProperty("shop").objectReferenceValue = shop;
            so.ApplyModifiedPropertiesWithoutUndo();

            SpriteVisualsSetup.AddVisual(merchant, sheet, material);
        }

        static void ExcludeLayerFromCamera(int layer)
        {
            var orbit = Object.FindFirstObjectByType<ThirdPersonCamera>();
            if (orbit == null) return;
            var so = new SerializedObject(orbit);
            var mask = so.FindProperty("collisionMask");
            mask.intValue &= ~(1 << layer);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static ItemData LoadItem(string name) => AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/{name}.asset");

        static ShopData.StockEntry Stock(ItemData item, int dailyStock) => new() { Item = item, DailyStock = dailyStock };

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
