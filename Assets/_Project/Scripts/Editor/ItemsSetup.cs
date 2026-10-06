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
    /// Milestone 4 setup: starter items, loot tables for the Bandit and Dummy, and the player's
    /// Inventory / quick-use / inventory screen in World_Test. Safe to re-run; existing assets keep your tuning.
    /// </summary>
    public static class ItemsSetup
    {
        const string Root = "Assets/_Project";
        const string ItemsFolder = Root + "/Data/Items";
        const string LootFolder = Root + "/Data/Items/Loot";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";

        [MenuItem("Beast/Setup/Run Milestone 4 Setup (Items)", priority = 3)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!System.IO.File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test scene not found. Run the earlier setups first.");
                return;
            }

            EnsureFolder(ItemsFolder);
            EnsureFolder(LootFolder);

            var bread = Asset<ConsumableData>(ItemsFolder, "Item_Bread", i =>
            {
                Configure(i, "Bread", ItemCategory.Consumable, "Dense frontier bread. Restores a little health.", new Color(0.85f, 0.65f, 0.35f), 20, 5);
                i.Heal = 30f;
            });
            var draught = Asset<ConsumableData>(ItemsFolder, "Item_HealingDraught", i =>
            {
                Configure(i, "Healing Draught", ItemCategory.Consumable, "Herbal tonic brewed from homestead crops.", new Color(0.85f, 0.2f, 0.25f), 10, 25);
                i.Heal = 60f;
                i.Stamina = 50f;
            });
            var cloth = Asset<ItemData>(ItemsFolder, "Item_BanditCloth", i =>
                Configure(i, "Bandit Cloth", ItemCategory.Material, "Rough, stained cloth. Useful for crafting and repairs.", new Color(0.45f, 0.3f, 0.2f), 50, 3));
            var iron = Asset<ItemData>(ItemsFolder, "Item_IronScrap", i =>
                Configure(i, "Iron Scrap", ItemCategory.Material, "Salvaged from broken war gear. Smiths pay well for it.", new Color(0.55f, 0.57f, 0.6f), 50, 8));
            var seeds = Asset<ItemData>(ItemsFolder, "Item_TurnipSeeds", i =>
                Configure(i, "Turnip Seeds", ItemCategory.Seed, "Hardy seeds that still sprout in poor soil.", new Color(0.4f, 0.75f, 0.3f), 99, 2));

            var banditLoot = Asset<LootTable>(LootFolder, "Loot_Bandit", l =>
            {
                l.Entries = new[]
                {
                    Entry(cloth, 0.8f, 1, 2),
                    Entry(iron, 0.35f, 1, 1),
                    Entry(seeds, 0.3f, 1, 3),
                    Entry(bread, 0.25f, 1, 1),
                };
                l.MinGold = 3;
                l.MaxGold = 12;
            });
            var dummyLoot = Asset<LootTable>(LootFolder, "Loot_TrainingDummy", l =>
            {
                l.Entries = new[] { Entry(bread, 0.5f, 1, 1), Entry(seeds, 0.3f, 1, 2) };
                l.MinGold = 1;
                l.MaxGold = 3;
            });

            AssignLoot(Root + "/Data/Combat/Enemies/Enemy_Bandit.asset", banditLoot);
            AssignLoot(Root + "/Data/Combat/Enemies/Enemy_TrainingDummy.asset", dummyLoot);

            string breadPath = AssetDatabase.GetAssetPath(bread);
            string draughtPath = AssetDatabase.GetAssetPath(draught);

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            bread = AssetDatabase.LoadAssetAtPath<ConsumableData>(breadPath);
            draught = AssetDatabase.LoadAssetAtPath<ConsumableData>(draughtPath);

            SetUpPlayer(bread, draught);
            foreach (var enemy in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!enemy.TryGetComponent(out LootDropper _)) enemy.gameObject.AddComponent<LootDropper>();
            if (Object.FindFirstObjectByType<InventoryScreen>() == null)
                new GameObject("InventoryUI").AddComponent<InventoryScreen>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Setup] Milestone 4 items setup complete. Press Play: kill enemies for loot, Tab opens the inventory, R eats food.");
        }

        static void SetUpPlayer(ConsumableData bread, ConsumableData draught)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No object tagged Player in World_Test.");
                return;
            }

            if (!player.TryGetComponent(out Inventory inventory))
            {
                inventory = player.AddComponent<Inventory>();
                var so = new SerializedObject(inventory);
                var start = so.FindProperty("startingItems");
                start.arraySize = 2;
                SetStartingItem(start.GetArrayElementAtIndex(0), bread, 3);
                SetStartingItem(start.GetArrayElementAtIndex(1), draught, 1);
                so.FindProperty("startingGold").intValue = 10;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (!player.TryGetComponent(out QuickItemUser _)) player.AddComponent<QuickItemUser>();
        }

        static void SetStartingItem(SerializedProperty element, ItemData item, int count)
        {
            element.FindPropertyRelative("Item").objectReferenceValue = item;
            element.FindPropertyRelative("Count").intValue = count;
        }

        static void AssignLoot(string enemyPath, LootTable loot)
        {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>(enemyPath);
            if (enemy == null || enemy.Loot != null) return;
            enemy.Loot = loot;
            EditorUtility.SetDirty(enemy);
        }

        static void Configure(ItemData item, string name, ItemCategory category, string description, Color color, int maxStack, int value)
        {
            item.DisplayName = name;
            item.Category = category;
            item.Description = description;
            item.PlaceholderColor = color;
            item.MaxStack = maxStack;
            item.BaseValue = value;
        }

        static LootTable.Entry Entry(ItemData item, float chance, int min, int max) =>
            new() { Item = item, Chance = chance, Min = min, Max = max };

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

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
