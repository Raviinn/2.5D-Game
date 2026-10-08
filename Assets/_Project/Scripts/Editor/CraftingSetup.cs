using System.Collections.Generic;
using System.IO;
using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 26 setup (crafting): new foods (Stamina Tonic, Roast Turnips, Turnip Stew), the starting recipes
    /// (alchemy, cooking, smithing), the recipe book, a workbench beside the homestead bed, and Player Buffs on the
    /// player. Safe to re-run: existing items and recipes are kept (your edits too); missing ones are added to the book.
    /// </summary>
    public static class CraftingSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string ItemFolder = Root + "/Data/Items";
        const string CraftFolder = Root + "/Data/Crafting";
        const string RecipeFolder = CraftFolder + "/Recipes";
        const string BookPath = CraftFolder + "/RecipeBook.asset";
        const string BenchName = "Workbench";

        [MenuItem("Beast/Setup/Run Milestone 26 Setup (Crafting)", priority = 22)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            EnsureFolder(CraftFolder);
            EnsureFolder(RecipeFolder);

            T Item<T>(string path) where T : ItemData
            {
                var item = AssetDatabase.LoadAssetAtPath<T>($"{ItemFolder}/{path}.asset");
                if (item == null) Debug.LogError($"[Setup] Missing item {path}. Run the earlier setups first.");
                return item;
            }

            var healroot = Item<ItemData>("Item_Healroot");
            var turnip = Item<ItemData>("Item_Turnip");
            var bread = Item<ItemData>("Item_Bread");
            var draught = Item<ItemData>("Item_HealingDraught");
            var scrap = Item<ItemData>("Item_IronScrap");
            var cloth = Item<ItemData>("Item_BanditCloth");
            var rustedSword = Item<ItemData>("Equipment/Weapon_RustedSwordShield");
            var ironSword = Item<ItemData>("Equipment/Weapon_IronSwordShield");
            var rustedGreat = Item<ItemData>("Equipment/Weapon_RustedGreatsword");
            var ironGreat = Item<ItemData>("Equipment/Weapon_IronGreatsword");
            var cap = Item<ItemData>("Equipment/Armor_PaddedCap");
            var helm = Item<ItemData>("Equipment/Armor_IronHelm");
            if (healroot == null || turnip == null || bread == null || draught == null || scrap == null || cloth == null ||
                rustedSword == null || ironSword == null || rustedGreat == null || ironGreat == null || cap == null || helm == null) return;

            int newItems = 0;
            var tonic = Food("Item_StaminaTonic", "Stamina Tonic", "A bitter green draught brewed from Healroot and turnip. Restores stamina and quickens your breath for a while.",
                new Color32(110, 180, 90, 255), 30, heal: 0f, stamina: 100f, new StatModifier(StatType.StaminaRegen, 0f, 0.3f), 120f, ref newItems);
            var roast = Food("Item_RoastTurnips", "Roast Turnips", "Turnips charred over the fire. Plain, filling.",
                new Color32(206, 150, 90, 255), 12, heal: 35f, stamina: 0f, default, 0f, ref newItems);
            var stew = Food("Item_TurnipStew", "Turnip Stew", "A thick stew with bread to dip. Heals well and steadies you against blows for a while.",
                new Color32(176, 112, 64, 255), 30, heal: 55f, stamina: 30f, new StatModifier(StatType.Defense, 15f), 180f, ref newItems);

            int newRecipes = 0;
            var recipes = new List<RecipeData>
            {
                Recipe("Recipe_HealingDraught", CraftKind.Alchemy, draught, 1, ref newRecipes, (healroot, 2)),
                Recipe("Recipe_StaminaTonic", CraftKind.Alchemy, tonic, 1, ref newRecipes, (healroot, 1), (turnip, 1)),
                Recipe("Recipe_RoastTurnips", CraftKind.Cooking, roast, 1, ref newRecipes, (turnip, 2)),
                Recipe("Recipe_TurnipStew", CraftKind.Cooking, stew, 1, ref newRecipes, (turnip, 2), (bread, 1)),
                Recipe("Recipe_IronSwordShield", CraftKind.Smithing, ironSword, 1, ref newRecipes, (rustedSword, 1), (scrap, 6), (cloth, 2)),
                Recipe("Recipe_IronGreatsword", CraftKind.Smithing, ironGreat, 1, ref newRecipes, (rustedGreat, 1), (scrap, 8), (cloth, 2)),
                Recipe("Recipe_PaddedCap", CraftKind.Smithing, cap, 1, ref newRecipes, (cloth, 4)),
                Recipe("Recipe_IronHelm", CraftKind.Smithing, helm, 1, ref newRecipes, (cap, 1), (scrap, 5)),
            };

            var book = AssetDatabase.LoadAssetAtPath<RecipeBook>(BookPath);
            if (book == null)
            {
                book = ScriptableObject.CreateInstance<RecipeBook>();
                book.Recipes = new RecipeData[0];
                AssetDatabase.CreateAsset(book, BookPath);
            }
            var list = new List<RecipeData>(book.Recipes ?? new RecipeData[0]);
            list.RemoveAll(r => r == null);
            foreach (var recipe in recipes) if (!list.Contains(recipe)) list.Add(recipe);
            book.Recipes = list.ToArray();
            EditorUtility.SetDirty(book);

            ItemIconsSetup.Run(); // icons for the new foods
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            var materialPaths = EnvironmentSetup.EnsureMaterials(EnvironmentArtGenerator.EnsureTextures());
            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            book = AssetDatabase.LoadAssetAtPath<RecipeBook>(BookPath);
            var wood = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Wood"]);
            var stone = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Stone"]);

            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No object tagged Player in World_Test.");
                return;
            }
            if (!player.TryGetComponent(out PlayerBuffs _)) player.AddComponent<PlayerBuffs>();

            string benchReport;
            var bench = GameObject.Find(BenchName);
            if (bench != null)
            {
                benchReport = "workbench kept";
            }
            else
            {
                var bed = Object.FindFirstObjectByType<SleepSpot>();
                if (bed == null)
                {
                    Debug.LogError("[Setup] No bed (Sleep Spot) in World_Test. Run the Milestone 5 setup first.");
                    return;
                }
                bench = BuildBench(FindBenchSpot(bed.transform), wood, stone);
                benchReport = $"workbench placed beside the bed at {bench.transform.position:F1}";
            }
            if (!bench.TryGetComponent(out Workbench benchComponent)) benchComponent = bench.AddComponent<Workbench>();
            var so = new SerializedObject(benchComponent);
            so.FindProperty("recipes").objectReferenceValue = book;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 26 crafting setup complete: {newItems} new food(s), {newRecipes} new recipe(s), " +
                      $"{book.Recipes.Length} in the book; {benchReport}; Player Buffs on the player.");
        }

        static ConsumableData Food(string asset, string displayName, string description, Color32 color, int value,
            float heal, float stamina, StatModifier buff, float buffSeconds, ref int created)
        {
            string path = $"{ItemFolder}/{asset}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ConsumableData>(path);
            if (existing != null) return existing;
            var food = ScriptableObject.CreateInstance<ConsumableData>();
            food.DisplayName = displayName;
            food.Description = description;
            food.Category = ItemCategory.Consumable;
            food.PlaceholderColor = color;
            food.BaseValue = value;
            food.MaxStack = 20;
            food.Heal = heal;
            food.Stamina = stamina;
            food.Buff = buff;
            food.BuffSeconds = buffSeconds;
            AssetDatabase.CreateAsset(food, path);
            created++;
            return food;
        }

        static RecipeData Recipe(string asset, CraftKind kind, ItemData output, int count, ref int created,
            params (ItemData item, int count)[] inputs)
        {
            string path = $"{RecipeFolder}/{asset}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
            if (existing != null) return existing;
            var recipe = ScriptableObject.CreateInstance<RecipeData>();
            recipe.Kind = kind;
            recipe.Output = output;
            recipe.OutputCount = count;
            recipe.Inputs = new RecipeData.Ingredient[inputs.Length];
            for (int i = 0; i < inputs.Length; i++) recipe.Inputs[i] = new RecipeData.Ingredient { Item = inputs[i].item, Count = inputs[i].count };
            AssetDatabase.CreateAsset(recipe, path);
            created++;
            return recipe;
        }

        /// <summary>A clear, level spot about two metres from the bed (tries each side, then further out).</summary>
        static Vector3 FindBenchSpot(Transform bed)
        {
            Physics.SyncTransforms();
            var directions = new[] { bed.right, -bed.right, -bed.forward, bed.forward };
            foreach (float distance in new[] { 2.2f, 3f, 4f })
                foreach (var direction in directions)
                {
                    var flat = new Vector3(direction.x, 0f, direction.z).normalized;
                    var spot = bed.position + flat * distance;
                    // From just above the bed's height, so a roof overhead is never mistaken for the floor.
                    var from = new Vector3(spot.x, bed.position.y + 1.5f, spot.z);
                    if (!Physics.Raycast(from, Vector3.down, out var ground, 4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var centre = ground.point + Vector3.up * 0.55f;
                    if (Physics.CheckBox(centre, new Vector3(1.1f, 0.45f, 0.7f), Quaternion.LookRotation(flat), ~0, QueryTriggerInteraction.Ignore)) continue;
                    return ground.point;
                }
            return bed.position + bed.right * 2.2f;
        }

        static GameObject BuildBench(Vector3 groundPoint, Material wood, Material stone)
        {
            var bench = new GameObject(BenchName);
            bench.transform.position = groundPoint;
            bench.isStatic = true;
            var collider = bench.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.45f, 0f);
            collider.size = new Vector3(1.6f, 0.9f, 0.8f);

            Part("Top", bench.transform, new Vector3(0f, 0.85f, 0f), new Vector3(1.6f, 0.1f, 0.8f), wood);
            foreach (var x in new[] { -0.7f, 0.7f })
                foreach (var z in new[] { -0.3f, 0.3f })
                    Part("Leg", bench.transform, new Vector3(x, 0.4f, z), new Vector3(0.1f, 0.8f, 0.1f), wood);
            Part("Shelf", bench.transform, new Vector3(0f, 0.25f, 0f), new Vector3(1.4f, 0.05f, 0.6f), wood);
            Part("Anvil", bench.transform, new Vector3(0.45f, 1.0f, 0f), new Vector3(0.4f, 0.2f, 0.25f), stone);
            Part("Pot", bench.transform, new Vector3(-0.45f, 0.99f, 0f), new Vector3(0.3f, 0.18f, 0.3f), stone);
            return bench;
        }

        static void Part(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            go.isStatic = true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
