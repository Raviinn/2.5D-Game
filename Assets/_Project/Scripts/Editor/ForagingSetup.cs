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
    /// Milestone 47 setup (foraging &amp; fishing): eight forage spots around the woods (wild garlic, berries,
    /// mushrooms, winterberries by season; they regrow in three days), a fishing pond with perch, trout and night pike,
    /// a Fishing Rod at Oswin's, four new recipes, and Player Fishing on the player.
    /// Safe to re-run: existing assets, the pond and the spots are kept.
    /// </summary>
    public static class ForagingSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string ItemFolder = Root + "/Data/Items";
        const string RecipeFolder = Root + "/Data/Crafting/Recipes";
        const string BookPath = Root + "/Data/Crafting/RecipeBook.asset";
        const string ShopPath = Root + "/Data/Economy/Shop_GeneralStore.asset";
        public const string PondName = "Fishing_Pond";
        public const string ForageRootName = "[Forage Spots]";
        const float PondRadius = 3f;

        static readonly Vector3[] PondCandidates = { new(-14f, 0f, 14f), new(-20f, 0f, 2f), new(-12f, 0f, -24f), new(8f, 0f, -22f), new(26f, 0f, -20f) };

        [MenuItem("Beast/Setup/Run Milestone 47 Setup (Foraging & Fishing)", priority = 39)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            var bread = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Item_Bread.asset");
            if (bread == null)
            {
                Debug.LogError("[Setup] Item_Bread not found. Run the earlier setups first.");
                return;
            }

            int created = 0;
            var garlic = Item("Item_WildGarlic", "Wild Garlic", "Broad leaves that smell of garlic. Spring, in the shade of the woods.", new Color32(214, 230, 196, 255), 7, ref created);
            var berries = Food("Item_WildBerries", "Wild Berries", "Dark summer berries. A handful takes the edge off.", new Color32(120, 40, 96, 255), 5, 8f, ref created);
            var mushrooms = Item("Item_ForestMushrooms", "Forest Mushrooms", "Brown-capped and earthy. Autumn's best gift.", new Color32(176, 116, 70, 255), 9, ref created);
            var winterberries = Food("Item_Winterberries", "Winterberries", "Bright red berries that cling on through the frost.", new Color32(206, 40, 50, 255), 8, 8f, ref created);
            var perch = Item("Item_RiverPerch", "River Perch", "A striped little fish. Good grilled.", new Color32(150, 160, 110, 255), 12, ref created);
            var trout = Item("Item_SilverTrout", "Silver Trout", "Quick and bright. Spring and summer only.", new Color32(180, 196, 210, 255), 28, ref created);
            var pike = Item("Item_NightPike", "Night Pike", "A long-jawed hunter that bites after dark in the cold months. Fetches a fine price.", new Color32(90, 110, 90, 255), 55, ref created);
            var rod = Asset<ItemData>($"{ItemFolder}/Item_FishingRod.asset", i =>
            {
                i.DisplayName = "Fishing Rod";
                i.Description = "Ash pole, horsehair line, a bent nail of a hook. Stand at the pond's edge and press F.";
                i.Category = ItemCategory.Material;
                i.PlaceholderColor = new Color32(150, 110, 60, 255);
                i.MaxStack = 1;
                i.BaseValue = 30;
            }, ref created);

            var grilled = Dish("Item_GrilledPerch", "Grilled Perch", "Charred skin, flaky inside.", new Color32(196, 150, 90, 255), 30, 40f, 20f, default, 0f, ref created);
            var baked = Dish("Item_HerbBakedTrout", "Herb-Baked Trout", "Trout baked with wild garlic. You feel sturdier for a while.", new Color32(206, 186, 150, 255), 70, 80f, 30f,
                new StatModifier(StatType.Defense, 10f), 240f, ref created);
            var skewers = Dish("Item_MushroomSkewers", "Mushroom Skewers", "Mushrooms roasted on a stick. Smoky.", new Color32(150, 100, 60, 255), 24, 35f, 30f, default, 0f, ref created);
            var tart = Dish("Item_BerryTart", "Berry Tart", "Wild berries baked into bread. Sweet; your breath comes easier.", new Color32(140, 50, 100, 255), 34, 45f, 20f,
                new StatModifier(StatType.StaminaRegen, 0f, 0.15f), 180f, ref created);

            var recipes = new[]
            {
                Recipe("Recipe_GrilledPerch", grilled, (perch, 1)),
                Recipe("Recipe_HerbBakedTrout", baked, (trout, 1), (garlic, 1)),
                Recipe("Recipe_MushroomSkewers", skewers, (mushrooms, 2)),
                Recipe("Recipe_BerryTart", tart, (berries, 3), (bread, 1)),
            };
            var book = AssetDatabase.LoadAssetAtPath<RecipeBook>(BookPath);
            if (book != null)
            {
                var list = new List<RecipeData>(book.Recipes ?? new RecipeData[0]);
                foreach (var recipe in recipes) if (!list.Contains(recipe)) list.Add(recipe);
                book.Recipes = list.ToArray();
                EditorUtility.SetDirty(book);
            }
            var shop = AssetDatabase.LoadAssetAtPath<ShopData>(ShopPath);
            if (shop != null && System.Array.FindIndex(shop.Stock ?? new ShopData.StockEntry[0], s => s.Item == rod) < 0)
            {
                shop.Stock = new List<ShopData.StockEntry>(shop.Stock ?? new ShopData.StockEntry[0]) { new() { Item = rod, DailyStock = 2 } }.ToArray();
                EditorUtility.SetDirty(shop);
            }

            ItemIconsSetup.Run();
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();
            var paths = new Dictionary<string, string>();
            foreach (var item in new[] { garlic, berries, mushrooms, winterberries, perch, trout, pike, rod }) paths[item.name] = AssetDatabase.GetAssetPath(item);

            // ---- Scene ----
            var materialPaths = EnvironmentSetup.EnsureMaterials(EnvironmentArtGenerator.EnsureTextures());
            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            ItemData Reload(string name) => AssetDatabase.LoadAssetAtPath<ItemData>(paths[name]);
            var water = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Water"]);
            var stone = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Stone"]);
            var dirt = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Dirt"]);
            Physics.SyncTransforms();

            var player = GameObject.FindWithTag("Player");
            if (player != null && !player.TryGetComponent(out PlayerFishing _)) player.AddComponent<PlayerFishing>();

            string pondReport = "pond kept";
            if (GameObject.Find(PondName) == null)
            {
                var centre = ChoosePond();
                BuildPond(centre, water, stone, Reload("Item_RiverPerch"), Reload("Item_SilverTrout"), Reload("Item_NightPike"), Reload("Item_FishingRod"));
                pondReport = $"pond at {centre:F0}";
            }

            string spotReport = "forage spots kept";
            if (GameObject.Find(ForageRootName) == null)
            {
                int placed = BuildForageSpots(dirt, Reload("Item_WildGarlic"), Reload("Item_WildBerries"), Reload("Item_ForestMushrooms"), Reload("Item_Winterberries"));
                spotReport = $"{placed} forage spots";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 47 foraging & fishing setup complete: {created} new asset(s); {pondReport}; {spotReport}; " +
                      "Oswin sells a Fishing Rod. New recipes: Grilled Perch, Herb-Baked Trout, Mushroom Skewers, Berry Tart.");
        }

        static Vector3 ChoosePond()
        {
            foreach (var c in PondCandidates)
                if (Physics.OverlapBox(c + Vector3.up * 1.2f, new Vector3(PondRadius + 2.2f, 1f, PondRadius + 2.2f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore).Length == 0)
                    return c;
            return PondCandidates[0];
        }

        static void BuildPond(Vector3 centre, Material water, Material stone, ItemData perch, ItemData trout, ItemData pike, ItemData rod)
        {
            var pond = new GameObject(PondName);
            pond.transform.position = centre;
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            surface.name = "Water";
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetParent(pond.transform, false);
            surface.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            surface.transform.localScale = new Vector3(PondRadius * 2f, 0.02f, PondRadius * 2f);
            surface.GetComponent<MeshRenderer>().sharedMaterial = water;
            surface.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            // A ring of stones around the bank.
            for (int i = 0; i < 22; i++)
            {
                float angle = i / 22f * Mathf.PI * 2f;
                var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rock.name = "Bank";
                Object.DestroyImmediate(rock.GetComponent<Collider>());
                rock.transform.SetParent(pond.transform, false);
                rock.transform.localPosition = new Vector3(Mathf.Cos(angle) * (PondRadius + 0.15f), 0.06f, Mathf.Sin(angle) * (PondRadius + 0.15f));
                rock.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + (i % 3) * 17f, 0f);
                rock.transform.localScale = new Vector3(0.55f + (i % 2) * 0.2f, 0.16f + (i % 3) * 0.04f, 0.4f);
                rock.GetComponent<MeshRenderer>().sharedMaterial = stone;
                rock.isStatic = true;
            }
            // Keeps you out of the water.
            var block = pond.AddComponent<CapsuleCollider>();
            block.center = new Vector3(0f, 1f, 0f);
            block.radius = PondRadius - 0.35f;
            block.height = 2f;

            var spot = pond.AddComponent<FishingSpot>();
            var so = new SerializedObject(spot);
            so.FindProperty("rod").objectReferenceValue = rod;
            so.FindProperty("radius").floatValue = PondRadius;
            var catches = so.FindProperty("catches");
            catches.arraySize = 3;
            SetCatch(catches.GetArrayElementAtIndex(0), perch, new Season[0], false, 6f, 0.2f);
            SetCatch(catches.GetArrayElementAtIndex(1), trout, new[] { Season.Spring, Season.Summer }, false, 3f, 0.5f);
            SetCatch(catches.GetArrayElementAtIndex(2), pike, new[] { Season.Autumn, Season.Winter }, true, 3f, 0.8f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetCatch(SerializedProperty element, ItemData fish, Season[] seasons, bool nightOnly, float weight, float difficulty)
        {
            element.FindPropertyRelative("Fish").objectReferenceValue = fish;
            var list = element.FindPropertyRelative("Seasons");
            list.arraySize = seasons.Length;
            for (int i = 0; i < seasons.Length; i++) list.GetArrayElementAtIndex(i).enumValueIndex = (int)seasons[i];
            element.FindPropertyRelative("NightOnly").boolValue = nightOnly;
            element.FindPropertyRelative("Weight").floatValue = weight;
            element.FindPropertyRelative("Difficulty").floatValue = difficulty;
        }

        static int BuildForageSpots(Material material, ItemData garlic, ItemData berries, ItemData mushrooms, ItemData winterberries)
        {
            var root = new GameObject(ForageRootName).transform;
            int placed = 0;
            for (int i = 0; i < 8; i++)
            {
                float angle = (i * 45f + 20f) * Mathf.Deg2Rad;
                Vector3? spot = null;
                foreach (float radius in new[] { 27f, 24f, 21f, 29f })
                {
                    var p = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (!Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y) > 0.3f) continue;
                    if (Physics.CheckSphere(hit.point + Vector3.up * 1.4f, 1.2f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    spot = hit.point;
                    break;
                }
                if (!spot.HasValue) continue;

                var go = new GameObject($"Forage_{i + 1}");
                go.transform.SetParent(root, false);
                go.transform.position = spot.Value;
                var parts = new List<Renderer>();
                for (int k = 0; k < 3; k++)
                {
                    var bit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    bit.name = "Bit";
                    Object.DestroyImmediate(bit.GetComponent<Collider>());
                    bit.transform.SetParent(go.transform, false);
                    float a = k * 2.1f + i;
                    bit.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.22f, 0.1f + k * 0.03f, Mathf.Sin(a) * 0.22f);
                    bit.transform.localScale = new Vector3(0.24f, 0.18f, 0.24f);
                    var renderer = bit.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    parts.Add(renderer);
                }
                var forage = go.AddComponent<ForageSpot>();
                var so = new SerializedObject(forage);
                so.FindProperty("saveId").stringValue = $"forage.spot{i + 1}";
                var list = so.FindProperty("parts");
                list.arraySize = parts.Count;
                for (int k = 0; k < parts.Count; k++) list.GetArrayElementAtIndex(k).objectReferenceValue = parts[k];
                var finds = so.FindProperty("finds");
                finds.arraySize = 4;
                SetFind(finds.GetArrayElementAtIndex(0), Season.Spring, garlic, 1, 2);
                SetFind(finds.GetArrayElementAtIndex(1), Season.Summer, berries, 2, 3);
                SetFind(finds.GetArrayElementAtIndex(2), Season.Autumn, mushrooms, 1, 3);
                SetFind(finds.GetArrayElementAtIndex(3), Season.Winter, winterberries, 1, 2);
                so.ApplyModifiedPropertiesWithoutUndo();
                placed++;
            }
            return placed;
        }

        static void SetFind(SerializedProperty element, Season season, ItemData item, int min, int max)
        {
            element.FindPropertyRelative("Season").enumValueIndex = (int)season;
            element.FindPropertyRelative("Item").objectReferenceValue = item;
            element.FindPropertyRelative("Min").intValue = min;
            element.FindPropertyRelative("Max").intValue = max;
        }

        static ItemData Item(string asset, string displayName, string description, Color32 color, int value, ref int created) =>
            Asset<ItemData>($"{ItemFolder}/{asset}.asset", i =>
            {
                i.DisplayName = displayName;
                i.Description = description;
                i.Category = ItemCategory.Crop;
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
                c.MaxStack = 99;
                c.BaseValue = value;
                c.Heal = heal;
            }, ref created);

        static ConsumableData Dish(string asset, string displayName, string description, Color32 color, int value, float heal, float stamina,
            StatModifier buff, float buffSeconds, ref int created) =>
            Asset<ConsumableData>($"{ItemFolder}/{asset}.asset", c =>
            {
                c.DisplayName = displayName;
                c.Description = description;
                c.Category = ItemCategory.Consumable;
                c.PlaceholderColor = color;
                c.MaxStack = 20;
                c.BaseValue = value;
                c.Heal = heal;
                c.Stamina = stamina;
                c.Buff = buff;
                c.BuffSeconds = buffSeconds;
            }, ref created);

        static RecipeData Recipe(string asset, ItemData output, params (ItemData item, int count)[] inputs)
        {
            int unused = 0;
            return Asset<RecipeData>($"{RecipeFolder}/{asset}.asset", r =>
            {
                r.Kind = CraftKind.Cooking;
                r.Output = output;
                r.OutputCount = 1;
                r.Inputs = new RecipeData.Ingredient[inputs.Length];
                for (int i = 0; i < inputs.Length; i++) r.Inputs[i] = new RecipeData.Ingredient { Item = inputs[i].item, Count = inputs[i].count };
            }, ref unused);
        }

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
