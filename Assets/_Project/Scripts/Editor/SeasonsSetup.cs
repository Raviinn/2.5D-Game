using System.Collections.Generic;
using System.IO;
using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 46 setup (seasons): four 14-day seasons (Spring, Summer, Autumn, Winter). Turnips and Healroot grow
    /// spring to autumn; new Pumpkins (autumn) and Frost Kale (winter); Pumpkin Soup; Oswin sells the new seeds.
    /// Adds the Season Keeper (season changes, festivals and the seasonal look) to World_Test and recompiles the Ink
    /// story (season() and festival()). Safe to re-run: existing assets keep your tuning.
    /// </summary>
    public static class SeasonsSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string ItemFolder = Root + "/Data/Items";
        const string CropFolder = Root + "/Data/Farming";
        const string CropArtFolder = Root + "/Art/Crops/Placeholder";
        const string RecipeFolder = Root + "/Data/Crafting/Recipes";
        const string BookPath = Root + "/Data/Crafting/RecipeBook.asset";
        const string ShopPath = Root + "/Data/Economy/Shop_GeneralStore.asset";
        const string KeeperName = "[Seasons]";

        static readonly Season[] GrowingSeasons = { Season.Spring, Season.Summer, Season.Autumn };

        [MenuItem("Beast/Setup/Run Milestone 46 Setup (Seasons)", priority = 38)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            var turnipCrop = AssetDatabase.LoadAssetAtPath<CropData>(CropFolder + "/Crop_Turnip.asset");
            var healrootCrop = AssetDatabase.LoadAssetAtPath<CropData>(CropFolder + "/Crop_Healroot.asset");
            var turnip = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Item_Turnip.asset");
            if (turnipCrop == null || healrootCrop == null || turnip == null)
            {
                Debug.LogError("[Setup] Turnip / Healroot crops not found. Run the Milestone 5 setup first.");
                return;
            }
            foreach (var crop in new[] { turnipCrop, healrootCrop })
            {
                if (crop.Seasons != null && crop.Seasons.Length > 0) continue;
                crop.Seasons = GrowingSeasons;
                EditorUtility.SetDirty(crop);
            }

            int created = 0;
            var pumpkin = Item("Item_Pumpkin", "Pumpkin", "A fat orange pumpkin. Sells well; makes a fine soup.", ItemCategory.Crop, new Color32(230, 130, 40, 255), 40, ref created);
            var pumpkinSeeds = Item("Item_PumpkinSeeds", "Pumpkin Seeds", "Flat white seeds. An autumn crop, slow to grow.", ItemCategory.Seed, new Color32(236, 220, 170, 255), 8, ref created);
            var kale = Food("Item_FrostKale", "Frost Kale", "Leaves that only sweeten in the cold. Eat it raw if you must.", new Color32(110, 170, 150, 255), 18, 25f, ref created);
            var kaleSeeds = Item("Item_FrostKaleSeeds", "Frost Kale Seeds", "Tiny dark seeds. The only thing that grows in winter.", ItemCategory.Seed, new Color32(70, 100, 90, 255), 5, ref created);
            var soup = Asset<ConsumableData>($"{ItemFolder}/Item_PumpkinSoup.asset", c =>
            {
                c.DisplayName = "Pumpkin Soup";
                c.Description = "Thick, sweet and hot. Keeps you going a long while.";
                c.Category = ItemCategory.Consumable;
                c.PlaceholderColor = new Color32(226, 140, 60, 255);
                c.MaxStack = 20;
                c.BaseValue = 60;
                c.Heal = 65f;
                c.Stamina = 40f;
                c.Buff = new StatModifier(StatType.MaxStamina, 20f);
                c.BuffSeconds = 240f;
            }, ref created);

            var pumpkinSheet = CropSpriteGenerator.CreateSheet($"{CropArtFolder}/Pumpkin_Placeholder.png", 4, new CropSpriteGenerator.Palette
            {
                Leaf = new Color32(80, 150, 60, 255), LeafDark = new Color32(50, 105, 40, 255),
                Produce = new Color32(236, 132, 36, 255), ProduceDark = new Color32(170, 80, 20, 255), Outline = new Color32(40, 30, 16, 255),
            });
            var kaleSheet = CropSpriteGenerator.CreateSheet($"{CropArtFolder}/FrostKale_Placeholder.png", 3, new CropSpriteGenerator.Palette
            {
                Leaf = new Color32(96, 156, 150, 255), LeafDark = new Color32(56, 110, 106, 255),
                Produce = new Color32(176, 214, 204, 255), ProduceDark = new Color32(110, 160, 150, 255), Outline = new Color32(20, 36, 34, 255),
                ProduceOnTop = true,
            });
            Asset<CropData>($"{CropFolder}/Crop_Pumpkin.asset", c =>
            {
                c.DisplayName = "Pumpkin";
                c.Seed = pumpkinSeeds;
                c.Produce = pumpkin;
                c.ProduceMin = 1;
                c.ProduceMax = 2;
                c.DaysPerStage = new[] { 2, 2, 2, 2 }; // 8 watered days
                c.HarvestXp = 14;
                c.Seasons = new[] { Season.Autumn };
                c.Sheet = pumpkinSheet;
            }, ref created);
            Asset<CropData>($"{CropFolder}/Crop_FrostKale.asset", c =>
            {
                c.DisplayName = "Frost Kale";
                c.Seed = kaleSeeds;
                c.Produce = kale;
                c.ProduceMin = 1;
                c.ProduceMax = 2;
                c.DaysPerStage = new[] { 1, 2, 2 }; // 5 watered days
                c.HarvestXp = 8;
                c.Seasons = new[] { Season.Winter };
                c.Sheet = kaleSheet;
            }, ref created);

            var recipe = Asset<RecipeData>($"{RecipeFolder}/Recipe_PumpkinSoup.asset", r =>
            {
                r.Kind = CraftKind.Cooking;
                r.Output = soup;
                r.OutputCount = 1;
                r.Inputs = new[] { new RecipeData.Ingredient { Item = pumpkin, Count = 1 }, new RecipeData.Ingredient { Item = turnip, Count = 1 } };
            }, ref created);
            var book = AssetDatabase.LoadAssetAtPath<RecipeBook>(BookPath);
            if (book != null && System.Array.IndexOf(book.Recipes ?? new RecipeData[0], recipe) < 0)
            {
                book.Recipes = new List<RecipeData>(book.Recipes ?? new RecipeData[0]) { recipe }.ToArray();
                EditorUtility.SetDirty(book);
            }
            var shop = AssetDatabase.LoadAssetAtPath<ShopData>(ShopPath);
            if (shop != null)
            {
                var stock = new List<ShopData.StockEntry>(shop.Stock ?? new ShopData.StockEntry[0]);
                foreach (var seed in new[] { pumpkinSeeds, kaleSeeds })
                    if (stock.FindIndex(s => s.Item == seed) < 0) stock.Add(new ShopData.StockEntry { Item = seed, DailyStock = 8 });
                shop.Stock = stock.ToArray();
                EditorUtility.SetDirty(shop);
            }

            ItemIconsSetup.Run();
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();
            if (!DialogueQuestSetup.CompileInk()) return;

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            bool added = GameObject.Find(KeeperName) == null;
            if (added) new GameObject(KeeperName).AddComponent<SeasonKeeper>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 46 seasons setup complete: {created} new asset(s); Season Keeper {(added ? "added" : "kept")}. " +
                      "Seasons last 14 days (Spring 8: Planting Festival, Autumn 14: Harvest Fair). Turnips and Healroot grow spring to autumn, " +
                      "Pumpkins in autumn, Frost Kale in winter; Oswin sells the new seeds.");
        }

        static ItemData Item(string asset, string displayName, string description, ItemCategory category, Color32 color, int value, ref int created) =>
            Asset<ItemData>($"{ItemFolder}/{asset}.asset", i =>
            {
                i.DisplayName = displayName;
                i.Description = description;
                i.Category = category;
                i.PlaceholderColor = color;
                i.MaxStack = 99;
                i.BaseValue = value;
            }, ref created);

        static ConsumableData Food(string asset, string displayName, string description, Color32 color, int value, float heal, ref int created) =>
            Asset<ConsumableData>($"{ItemFolder}/{asset}.asset", c =>
            {
                c.DisplayName = displayName;
                c.Description = description;
                c.Category = ItemCategory.Crop;
                c.PlaceholderColor = color;
                c.MaxStack = 50;
                c.BaseValue = value;
                c.Heal = heal;
            }, ref created);

        static T Asset<T>(string path, System.Action<T> configureNew, ref int created) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            configureNew(asset);
            AssetDatabase.CreateAsset(asset, path);
            created++;
            return asset;
        }
    }
}
