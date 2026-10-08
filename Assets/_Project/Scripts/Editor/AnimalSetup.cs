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
    /// Milestone 45 setup (animals): a fenced pen near the homestead with a coop, a feed trough, three hens and a cow.
    /// Fill the trough each day (Animal Feed from Oswin, or two turnips); next morning collect eggs and milk. Adds the
    /// items, three cooking recipes (Fried Eggs, Farmhouse Omelette, Warm Milk), and Animal Feed to Oswin's stock.
    /// Safe to re-run: existing assets and the pen are kept.
    /// </summary>
    public static class AnimalSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string ItemFolder = Root + "/Data/Items";
        const string RecipeFolder = Root + "/Data/Crafting/Recipes";
        const string BookPath = Root + "/Data/Crafting/RecipeBook.asset";
        const string ShopPath = Root + "/Data/Economy/Shop_GeneralStore.asset";
        const string SpriteMaterialPath = Root + "/Art/Materials/Sprite_Lit.mat";
        public const string PenName = "Animal_Pen";

        static readonly Vector2 PenHalf = new(4.5f, 3.5f);
        static readonly Vector3[] PenCandidates = { new(20f, 0f, -6f), new(20f, 0f, -13f), new(14f, 0f, -16f), new(-6f, 0f, -20f), new(24f, 0f, 2f) };
        static readonly string[] HenNames = { "Pip", "Marigold", "Dot" };

        [MenuItem("Beast/Setup/Run Milestone 45 Setup (Animals)", priority = 37)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            var turnip = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Item_Turnip.asset");
            if (turnip == null)
            {
                Debug.LogError("[Setup] Item_Turnip not found. Run the earlier setups first.");
                return;
            }

            int created = 0;
            var feed = Item("Item_AnimalFeed", "Animal Feed", "A sack of oats and chaff. One sackful fills the trough for a day.", ItemCategory.Material, new Color32(196, 170, 110, 255), 4, ref created);
            var egg = Item("Item_Egg", "Egg", "Still warm. Better fried.", ItemCategory.Crop, new Color32(236, 226, 200, 255), 6, ref created);
            var milk = Item("Item_Milk", "Milk", "A pail of Bess's milk. Creamy.", ItemCategory.Crop, new Color32(240, 240, 232, 255), 10, ref created);
            var friedEggs = Food("Item_FriedEggs", "Fried Eggs", "Two eggs, crisp at the edges.", new Color32(240, 200, 90, 255), 16, 30f, 20f, default, 0f, ref created);
            var omelette = Food("Item_FarmhouseOmelette", "Farmhouse Omelette", "Eggs, milk and turnip folded together. Hearty: for a while you can take more punishment.",
                new Color32(232, 190, 96, 255), 40, 70f, 30f, new StatModifier(StatType.MaxHealth, 20f), 240f, ref created);
            var warmMilk = Food("Item_WarmMilk", "Warm Milk", "Warmed by the fire. Your breath comes easier for a while.",
                new Color32(246, 240, 226, 255), 18, 0f, 60f, new StatModifier(StatType.StaminaRegen, 0f, 0.2f), 180f, ref created);

            var recipes = new[]
            {
                Recipe("Recipe_FriedEggs", CraftKind.Cooking, friedEggs, (egg, 2)),
                Recipe("Recipe_FarmhouseOmelette", CraftKind.Cooking, omelette, (egg, 2), (turnip, 1), (milk, 1)),
                Recipe("Recipe_WarmMilk", CraftKind.Cooking, warmMilk, (milk, 1)),
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
            if (shop != null && System.Array.FindIndex(shop.Stock ?? new ShopData.StockEntry[0], s => s.Item == feed) < 0)
            {
                var stock = new List<ShopData.StockEntry>(shop.Stock ?? new ShopData.StockEntry[0]) { new() { Item = feed, DailyStock = 10 } };
                shop.Stock = stock.ToArray();
                EditorUtility.SetDirty(shop);
            }

            string henSheet = SpriteVisualsSetup.CreateSheet("Hen", new PlaceholderSpriteGenerator.Palette
            {
                Body = new Color32(232, 226, 212, 255), Trim = new Color32(176, 132, 84, 255), Skin = new Color32(206, 52, 42, 255),
                Hair = new Color32(20, 18, 18, 255), Weapon = new Color32(232, 164, 60, 255), Outline = new Color32(40, 32, 28, 255),
                Creature = Creature.Hen,
            });
            string cowSheet = SpriteVisualsSetup.CreateSheet("Cow", new PlaceholderSpriteGenerator.Palette
            {
                Body = new Color32(236, 230, 220, 255), Trim = new Color32(62, 52, 48, 255), Skin = new Color32(222, 160, 160, 255),
                Hair = new Color32(20, 18, 18, 255), Weapon = new Color32(226, 214, 180, 255), Outline = new Color32(30, 26, 24, 255),
                Creature = Creature.Cow,
            });

            ItemIconsSetup.Run();
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            // ---- The pen ----
            var materialPaths = EnvironmentSetup.EnsureMaterials(EnvironmentArtGenerator.EnsureTextures());
            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            if (GameObject.Find(PenName) != null)
            {
                Debug.Log("[Setup] Milestone 45 animals setup complete: items and recipes checked; the pen already exists (kept as-is).");
                return;
            }
            Physics.SyncTransforms();
            var wood = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Wood"]);
            var dirt = AssetDatabase.LoadAssetAtPath<Material>(materialPaths["Env_Dirt"]);
            var roof = AssetDatabase.LoadAssetAtPath<Material>(materialPaths.TryGetValue("Env_Roof", out var roofPath) ? roofPath : materialPaths["Env_Wood"]);
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);
            feed = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Item_AnimalFeed.asset");
            egg = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Item_Egg.asset");
            milk = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Item_Milk.asset");
            turnip = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Item_Turnip.asset");

            Vector3 centre = ChooseSpot();
            var pen = new GameObject(PenName).transform;
            pen.position = centre;
            int npcLayer = LayerMask.NameToLayer("NPC");

            // Ground, fence with a gate facing the homestead, coop, trough.
            Part("PenGround", pen, new Vector3(0f, 0.01f, 0f), new Vector3(PenHalf.x * 2f, 0.02f, PenHalf.y * 2f), dirt, false);
            Vector3 toHome = new Vector3(11.5f, 0f, -4f) - centre;
            toHome.y = 0f;
            bool gateOnX = Mathf.Abs(toHome.x) * PenHalf.y > Mathf.Abs(toHome.z) * PenHalf.x;
            Vector3 gate = gateOnX ? new Vector3(Mathf.Sign(toHome.x) * PenHalf.x, 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(toHome.z) * PenHalf.y);
            BuildFence(pen, gate, wood);
            Vector3 coopLocal = new Vector3(-Mathf.Sign(gate.x == 0f ? 1f : gate.x) * (PenHalf.x - 1.3f), 0f, -Mathf.Sign(gate.z == 0f ? 1f : gate.z) * (PenHalf.y - 1.1f));
            var coop = new GameObject("Coop").transform;
            coop.SetParent(pen, false);
            coop.localPosition = coopLocal;
            Part("Hutch", coop, new Vector3(0f, 0.55f, 0f), new Vector3(1.8f, 1.1f, 1.4f), wood, true);
            Part("CoopRoof", coop, new Vector3(0f, 1.2f, 0f), new Vector3(2.1f, 0.18f, 1.7f), roof, false);
            Part("Ramp", coop, new Vector3(0f, 0.2f, 0.95f), new Vector3(0.5f, 0.06f, 0.6f), wood, false);

            var troughObject = new GameObject("Feed_Trough");
            troughObject.transform.SetParent(pen, false);
            troughObject.transform.localPosition = gate * 0.55f + (gateOnX ? new Vector3(0f, 0f, 1.3f) : new Vector3(1.3f, 0f, 0f));
            troughObject.transform.rotation = Quaternion.LookRotation(gateOnX ? Vector3.forward : Vector3.right);
            Part("Box", troughObject.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.5f, 1.6f), wood, true);
            var fill = Part("Feed", troughObject.transform, new Vector3(0f, 0.56f, 0f), new Vector3(0.45f, 0.04f, 1.45f), dirt, false);
            var trough = troughObject.AddComponent<FeedTrough>();
            var tso = new SerializedObject(trough);
            tso.FindProperty("feed").objectReferenceValue = feed;
            tso.FindProperty("fallbackFeed").objectReferenceValue = turnip;
            tso.FindProperty("fillVisual").objectReferenceValue = fill.gameObject;
            tso.ApplyModifiedPropertiesWithoutUndo();

            // The animals.
            var wander = new Vector2(PenHalf.x - 1f, PenHalf.y - 0.9f);
            var henSprites = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(henSheet);
            for (int i = 0; i < HenNames.Length; i++)
            {
                var local = new Vector3(-1.5f + i * 1.4f, 0f, 0.6f * (i % 2 == 0 ? 1f : -1f));
                Animal(pen, HenNames[i], AnimalKind.Hen, egg, $"animal.hen.{HenNames[i].ToLowerInvariant()}", centre + local, centre, wander, 1.1f,
                    0.25f, 0.6f, henSprites, spriteMaterial, npcLayer);
            }
            Animal(pen, "Bess", AnimalKind.Cow, milk, "animal.cow.bess", centre + new Vector3(1.2f, 0f, -1.2f), centre, wander, 0.6f,
                0.6f, 1.4f, AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(cowSheet), spriteMaterial, npcLayer);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 45 animals setup complete: {created} new item(s); a pen at {centre:F0} with three hens (Pip, Marigold, Dot), " +
                      "Bess the cow, a coop and a feed trough. Oswin sells Animal Feed; new cooking recipes: Fried Eggs, Farmhouse Omelette, Warm Milk.");
        }

        static Vector3 ChooseSpot()
        {
            foreach (var candidate in PenCandidates)
            {
                var hits = Physics.OverlapBox(candidate + Vector3.up * 1.2f, new Vector3(PenHalf.x + 0.8f, 1f, PenHalf.y + 0.8f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                if (hits.Length == 0) return candidate;
            }
            return PenCandidates[0];
        }

        static void BuildFence(Transform pen, Vector3 gateLocal, Material wood)
        {
            var corners = new[]
            {
                new Vector3(-PenHalf.x, 0f, -PenHalf.y), new Vector3(PenHalf.x, 0f, -PenHalf.y),
                new Vector3(PenHalf.x, 0f, PenHalf.y), new Vector3(-PenHalf.x, 0f, PenHalf.y),
            };
            const float gateHalf = 1.1f;
            var fence = new GameObject("Fence").transform;
            fence.SetParent(pen, false);
            for (int side = 0; side < 4; side++)
            {
                var a = corners[side];
                var b = corners[(side + 1) % 4];
                int spans = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / 1.5f));
                for (int i = 0; i < spans; i++)
                {
                    var post = Vector3.Lerp(a, b, i / (float)spans);
                    var next = Vector3.Lerp(a, b, (i + 1) / (float)spans);
                    if ((post - gateLocal).magnitude >= gateHalf) Part("Post", fence, post + Vector3.up * 0.5f, new Vector3(0.14f, 1f, 0.14f), wood, true);
                    var mid = (post + next) * 0.5f;
                    if ((mid - gateLocal).magnitude < gateHalf + 0.2f) continue;
                    foreach (float h in new[] { 0.42f, 0.8f })
                    {
                        var rail = Part("Rail", fence, mid + Vector3.up * h, new Vector3(0.06f, 0.08f, Vector3.Distance(post, next)), wood, true);
                        rail.localRotation = Quaternion.LookRotation(next - post);
                    }
                }
            }
        }

        static void Animal(Transform pen, string name, AnimalKind kind, ItemData product, string saveId, Vector3 position, Vector3 penCentre,
            Vector2 wander, float speed, float radius, float height, DirectionalSpriteSheet sheet, Material material, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(pen, true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            if (layer >= 0) go.layer = layer;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
            var animal = go.AddComponent<FarmAnimal>();
            var so = new SerializedObject(animal);
            so.FindProperty("kind").enumValueIndex = (int)kind;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("product").objectReferenceValue = product;
            so.FindProperty("saveId").stringValue = saveId;
            so.FindProperty("penCentre").vector3Value = penCentre;
            so.FindProperty("penHalfSize").vector2Value = wander;
            so.FindProperty("walkSpeed").floatValue = speed;
            so.ApplyModifiedPropertiesWithoutUndo();
            SpriteVisualsSetup.AddVisual(go, sheet, material);
            var visual = go.transform.Find("Visual");
            if (visual != null) visual.localPosition = Vector3.zero; // the root stands on the ground
        }

        static Transform Part(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material, bool collider)
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
            go.isStatic = true;
            return go.transform;
        }

        static ItemData Item(string asset, string displayName, string description, ItemCategory category, Color32 color, int value, ref int created)
        {
            string path = $"{ItemFolder}/{asset}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (existing != null) return existing;
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.DisplayName = displayName;
            item.Description = description;
            item.Category = category;
            item.PlaceholderColor = color;
            item.MaxStack = 99;
            item.BaseValue = value;
            AssetDatabase.CreateAsset(item, path);
            created++;
            return item;
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

        static RecipeData Recipe(string asset, CraftKind kind, ItemData output, params (ItemData item, int count)[] inputs)
        {
            string path = $"{RecipeFolder}/{asset}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
            if (existing != null) return existing;
            var recipe = ScriptableObject.CreateInstance<RecipeData>();
            recipe.Kind = kind;
            recipe.Output = output;
            recipe.OutputCount = 1;
            recipe.Inputs = new RecipeData.Ingredient[inputs.Length];
            for (int i = 0; i < inputs.Length; i++) recipe.Inputs[i] = new RecipeData.Ingredient { Item = inputs[i].item, Count = inputs[i].count };
            AssetDatabase.CreateAsset(recipe, path);
            return recipe;
        }
    }
}
