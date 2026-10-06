using System;
using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 5 setup: Turnip + Healroot crops (items, crop data, placeholder art), a 6×6 field and a bed
    /// at the test homestead, and farming components + starter seeds on the player. Safe to re-run.
    /// </summary>
    public static class FarmingSetup
    {
        const string Root = "Assets/_Project";
        const string ItemsFolder = Root + "/Data/Items";
        const string CropsFolder = Root + "/Data/Farming";
        const string CropArtFolder = Root + "/Art/Crops/Placeholder";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string SpriteMaterialPath = Root + "/Art/Materials/Sprite_Lit.mat";

        [MenuItem("Beast/Setup/Run Milestone 5 Setup (Farming)", priority = 4)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!System.IO.File.Exists(TestWorldScenePath) || AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath) == null)
            {
                Debug.LogError("[Setup] Run the Milestone 1–4 setups first (World_Test scene and Sprite_Lit material are required).");
                return;
            }

            EnsureFolder(ItemsFolder);
            EnsureFolder(CropsFolder);
            EnsureFolder(CropArtFolder);

            // Items
            var turnipSeeds = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsFolder}/Item_TurnipSeeds.asset");
            if (turnipSeeds == null)
            {
                Debug.LogError("[Setup] Item_TurnipSeeds not found. Run Milestone 4 Setup (Items) first.");
                return;
            }
            var turnip = Asset<ConsumableData>(ItemsFolder, "Item_Turnip", i =>
            {
                Configure(i, "Turnip", ItemCategory.Crop, "Crunchy and filling. Sells well at market, or eat it to heal.", new Color(0.85f, 0.75f, 0.9f), 50, 12);
                i.Heal = 15f;
            });
            var healroot = Asset<ItemData>(ItemsFolder, "Item_Healroot", i =>
                Configure(i, "Healroot", ItemCategory.Crop, "Bitter red berries. The key ingredient in healing draughts.", new Color(0.8f, 0.15f, 0.2f), 50, 20));
            var healrootSeeds = Asset<ItemData>(ItemsFolder, "Item_HealrootSeeds", i =>
                Configure(i, "Healroot Seeds", ItemCategory.Seed, "Seeds of a hardy medicinal shrub. Regrows after harvest.", new Color(0.6f, 0.3f, 0.3f), 99, 6));

            // Crops (placeholder art first, so the crop asset can reference it)
            var turnipSheet = CropSpriteGenerator.CreateSheet($"{CropArtFolder}/Turnip_Placeholder.png", 3, new CropSpriteGenerator.Palette
            {
                Leaf = new Color32(96, 170, 70, 255), LeafDark = new Color32(60, 120, 45, 255),
                Produce = new Color32(230, 220, 240, 255), ProduceDark = new Color32(150, 90, 170, 255), Outline = new Color32(30, 40, 20, 255),
            });
            var healrootSheet = CropSpriteGenerator.CreateSheet($"{CropArtFolder}/Healroot_Placeholder.png", 3, new CropSpriteGenerator.Palette
            {
                Leaf = new Color32(70, 140, 90, 255), LeafDark = new Color32(40, 95, 60, 255),
                Produce = new Color32(220, 40, 50, 255), ProduceDark = new Color32(140, 20, 30, 255), Outline = new Color32(20, 35, 25, 255),
                ProduceOnTop = true,
            });

            Asset<CropData>(CropsFolder, "Crop_Turnip", c =>
            {
                c.DisplayName = "Turnip";
                c.Seed = turnipSeeds;
                c.Produce = turnip;
                c.ProduceMin = 1;
                c.ProduceMax = 2;
                c.DaysPerStage = new[] { 1, 1, 2 }; // 4 watered days
                c.RegrowDays = 0;
                c.Sheet = turnipSheet;
            });
            Asset<CropData>(CropsFolder, "Crop_Healroot", c =>
            {
                c.DisplayName = "Healroot";
                c.Seed = healrootSeeds;
                c.Produce = healroot;
                c.ProduceMin = 1;
                c.ProduceMax = 3;
                c.DaysPerStage = new[] { 2, 2, 2 }; // 6 watered days
                c.RegrowDays = 3;
                c.Sheet = healrootSheet;
            });

            string turnipSeedsPath = AssetDatabase.GetAssetPath(turnipSeeds);
            string healrootSeedsPath = AssetDatabase.GetAssetPath(healrootSeeds);

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            turnipSeeds = AssetDatabase.LoadAssetAtPath<ItemData>(turnipSeedsPath);
            healrootSeeds = AssetDatabase.LoadAssetAtPath<ItemData>(healrootSeedsPath);
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);

            SetUpPlayer(turnipSeeds, healrootSeeds);
            BuildHomestead(spriteMaterial);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Setup] Milestone 5 farming setup complete. The field is just south-east of the spawn; the bed is beside it.");
        }

        static void SetUpPlayer(ItemData turnipSeeds, ItemData healrootSeeds)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No object tagged Player in World_Test.");
                return;
            }
            if (!player.TryGetComponent(out Inventory inventory))
            {
                Debug.LogError("[Setup] Player has no Inventory. Run Milestone 4 Setup (Items) first.");
                return;
            }

            if (!player.TryGetComponent(out PlayerInteractor _)) player.AddComponent<PlayerInteractor>();
            if (!player.TryGetComponent(out PlayerFarmer _)) player.AddComponent<PlayerFarmer>();

            // Starter seeds, so farming can be tested without hunting bandits first.
            var so = new SerializedObject(inventory);
            var start = so.FindProperty("startingItems");
            AddStartingItem(start, turnipSeeds, 6);
            AddStartingItem(start, healrootSeeds, 3);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddStartingItem(SerializedProperty list, ItemData item, int count)
        {
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("Item").objectReferenceValue == item) return;

            list.arraySize++;
            var element = list.GetArrayElementAtIndex(list.arraySize - 1);
            element.FindPropertyRelative("Item").objectReferenceValue = item;
            element.FindPropertyRelative("Count").intValue = count;
        }

        static void BuildHomestead(Material spriteMaterial)
        {
            if (GameObject.Find("Homestead") != null)
            {
                Debug.Log("[Setup] Homestead already exists in World_Test; kept as-is.");
                return;
            }

            var homestead = new GameObject("Homestead").transform;

            // Field: 6×6 tiles with its corner at (4, 0, -7), so it spans x 4–10, z -7 to -1.
            var fieldObject = new GameObject("Field");
            fieldObject.transform.SetParent(homestead, false);
            fieldObject.transform.position = new Vector3(4f, 0f, -7f);
            var field = fieldObject.AddComponent<FarmPlot>();
            var so = new SerializedObject(field);
            so.FindProperty("cropMaterial").objectReferenceValue = spriteMaterial;
            so.ApplyModifiedPropertiesWithoutUndo();

            var fieldBase = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fieldBase.name = "FieldBase";
            Object.DestroyImmediate(fieldBase.GetComponent<Collider>());
            fieldBase.transform.SetParent(homestead, false);
            fieldBase.transform.position = new Vector3(7f, 0.005f, -4f);
            fieldBase.transform.localScale = new Vector3(6.4f, 0.01f, 6.4f);
            fieldBase.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMaterial("Greybox_FieldBase", new Color(0.3f, 0.33f, 0.2f));
            fieldBase.isStatic = true;

            var bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bed.name = "Bed";
            bed.transform.SetParent(homestead, false);
            bed.transform.position = new Vector3(11.5f, 0.25f, -4f);
            bed.transform.localScale = new Vector3(1f, 0.5f, 2f);
            bed.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMaterial("Greybox_Bed", new Color(0.55f, 0.25f, 0.25f));
            bed.AddComponent<SleepSpot>();
        }

        // ---------- Helpers ----------

        static void Configure(ItemData item, string name, ItemCategory category, string description, Color color, int maxStack, int value)
        {
            item.DisplayName = name;
            item.Category = category;
            item.Description = description;
            item.PlaceholderColor = color;
            item.MaxStack = maxStack;
            item.BaseValue = value;
        }

        /// <summary>Loads the asset, or creates it and applies the defaults. Existing assets keep your tuning.</summary>
        static T Asset<T>(string folder, string name, Action<T> configureNew) where T : ScriptableObject
        {
            string path = $"{folder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            configureNew(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = $"{Root}/Art/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
