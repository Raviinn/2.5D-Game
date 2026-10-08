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
    /// Milestone 40 setup (new enemy types):
    /// - a pack of three Grey Wolves in the north-west woods: fast, flanking, darting away after each bite;
    /// - a Bandit Shieldbearer at the bandit camp: blocks from the front until its guard breaks;
    /// - a Blighted Brute among the dead trees past the camp: huge, slow, unstoppable mid-swing, abroad only at night.
    /// Plus their attacks, sprites, loot (Wolf Pelt, Blight Ichor) and two recipes that use it (Fur Leggings,
    /// Ichor Draught). Safe to re-run: existing assets keep your tuning; existing enemies are re-linked, not duplicated.
    /// </summary>
    public static class EnemyTypesSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string CombatRoot = Root + "/Data/Combat";
        const string ItemFolder = Root + "/Data/Items";
        const string RecipeFolder = Root + "/Data/Crafting/Recipes";
        const string BookPath = Root + "/Data/Crafting/RecipeBook.asset";

        public const string WolfDataPath = CombatRoot + "/Enemies/Enemy_Wolf.asset";
        public const string ShieldDataPath = CombatRoot + "/Enemies/Enemy_BanditShield.asset";
        public const string BruteDataPath = CombatRoot + "/Enemies/Enemy_BlightBrute.asset";

        public static readonly string[] WolfNames = { "Wolf_A", "Wolf_B", "Wolf_C" };
        public const string ShieldName = "Bandit_Shieldbearer";
        public const string BruteName = "Blighted_Brute";

        static readonly Vector3 WolfDen = new(-24f, 0f, 24f);
        static readonly Vector3 ShieldSpot = new(21f, 0f, 15f);
        static readonly Vector3 BruteSpot = new(27f, 0f, 27f);

        static readonly PlaceholderSpriteGenerator.Palette WolfPalette = new()
        {
            Body = new Color32(118, 116, 112, 255), Trim = new Color32(74, 70, 68, 255), Skin = new Color32(196, 188, 172, 255),
            Hair = new Color32(232, 196, 70, 255), Weapon = new Color32(245, 240, 225, 255), Outline = new Color32(22, 20, 20, 255),
            Creature = Creature.Wolf,
        };

        static readonly PlaceholderSpriteGenerator.Palette ShieldPalette = new()
        {
            Body = new Color32(120, 62, 48, 255), Trim = new Color32(58, 44, 34, 255), Skin = new Color32(200, 150, 112, 255),
            Hair = new Color32(160, 128, 64, 255), Weapon = new Color32(190, 190, 180, 255), Outline = new Color32(24, 16, 14, 255),
            HasWeapon = true, HasShield = true,
        };

        static readonly PlaceholderSpriteGenerator.Palette BrutePalette = new()
        {
            Body = new Color32(82, 66, 96, 255), Trim = new Color32(48, 38, 58, 255), Skin = new Color32(150, 142, 164, 255),
            Hair = new Color32(36, 28, 44, 255), Weapon = new Color32(116, 104, 92, 255), Outline = new Color32(16, 12, 20, 255),
            HasWeapon = true, Greatsword = true,
        };

        [MenuItem("Beast/Setup/Run Milestone 40 Setup (New Enemy Types)", priority = 34)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            var bandit = AssetDatabase.LoadAssetAtPath<EnemyData>(CombatRoot + "/Enemies/Enemy_Bandit.asset");
            var banditSlash = AssetDatabase.LoadAssetAtPath<AttackData>(CombatRoot + "/Attacks/Bandit_Slash.asset");
            var scrap = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Item_IronScrap.asset");
            var cloth = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Item_BanditCloth.asset");
            var healroot = AssetDatabase.LoadAssetAtPath<ItemData>(ItemFolder + "/Item_Healroot.asset");
            if (bandit == null || banditSlash == null || scrap == null || cloth == null || healroot == null)
            {
                Debug.LogError("[Setup] Missing Bandit data or basic items. Run the earlier setups first.");
                return;
            }

            // ---- Items, loot and recipes ----
            int created = 0;
            var pelt = Material("Item_WolfPelt", "Wolf Pelt", "A thick grey pelt. Warm, tough, and in demand with anyone who sews.",
                new Color32(132, 128, 120, 255), 9, ref created);
            var ichor = Material("Item_BlightIchor", "Blight Ichor", "Black, faintly warm ooze from a blighted brute. Alchemists say it sharpens the blood. Handle with gloves.",
                new Color32(92, 60, 120, 255), 24, ref created);
            var leggings = Asset<EquipmentData>(ItemFolder + "/Equipment/Armor_FurLeggings.asset", e =>
            {
                e.DisplayName = "Fur Leggings";
                e.Description = "Wolf pelts stitched over cloth. Warm on night watches; the fur turns a blade more often than you'd think.";
                e.Category = ItemCategory.Equipment;
                e.Slot = EquipSlot.Legs;
                e.PlaceholderColor = new Color32(140, 132, 118, 255);
                e.MaxStack = 1;
                e.BaseValue = 55;
                e.Modifiers = new[] { new StatModifier(StatType.Defense, 9f), new StatModifier(StatType.MaxStamina, 10f) };
            }, ref created);
            var draught = Asset<ConsumableData>(ItemFolder + "/Item_IchorDraught.asset", c =>
            {
                c.DisplayName = "Ichor Draught";
                c.Description = "Blight Ichor cut with Healroot until it stops smoking. Your blows land harder for a while.";
                c.Category = ItemCategory.Consumable;
                c.PlaceholderColor = new Color32(120, 70, 150, 255);
                c.MaxStack = 20;
                c.BaseValue = 40;
                c.Heal = 10f;
                c.Buff = new StatModifier(StatType.AttackPower, 0f, 0.15f);
                c.BuffSeconds = 120f;
            }, ref created);

            var wolfLoot = Asset<LootTable>(ItemFolder + "/Loot/Loot_Wolf.asset", l =>
            {
                l.Entries = new[] { new LootTable.Entry { Item = pelt, Chance = 0.75f, Min = 1, Max = 1 } };
            }, ref created);
            var bruteLoot = Asset<LootTable>(ItemFolder + "/Loot/Loot_BlightBrute.asset", l =>
            {
                l.Entries = new[]
                {
                    new LootTable.Entry { Item = ichor, Chance = 1f, Min = 1, Max = 2 },
                    new LootTable.Entry { Item = scrap, Chance = 0.6f, Min = 2, Max = 4 },
                };
                l.MinGold = 20;
                l.MaxGold = 40;
            }, ref created);

            var newRecipes = new[]
            {
                Recipe("Recipe_FurLeggings", CraftKind.Smithing, leggings, (pelt, 3), (cloth, 1)),
                Recipe("Recipe_IchorDraught", CraftKind.Alchemy, draught, (ichor, 1), (healroot, 1)),
            };
            var book = AssetDatabase.LoadAssetAtPath<RecipeBook>(BookPath);
            if (book != null)
            {
                var list = new List<RecipeData>(book.Recipes ?? new RecipeData[0]);
                foreach (var recipe in newRecipes) if (!list.Contains(recipe)) list.Add(recipe);
                book.Recipes = list.ToArray();
                EditorUtility.SetDirty(book);
            }

            // ---- Attacks and enemy data ----
            var bite = Attack("Wolf_Bite", 9f, 14f, 2.5f, 0.4f, 0.1f, 0.3f, new Vector3(1.3f, 1.1f, 1.4f), 0.9f, 2.2f, 0.03f, 0.05f);
            var bash = Attack("Shield_Bash", 10f, 38f, 7f, 0.55f, 0.12f, 0.5f, new Vector3(1.4f, 1.3f, 1.3f), 0.9f, 1.0f, 0.06f, 0.15f);
            var slam = Attack("Brute_Slam", 32f, 60f, 9f, 1.0f, 0.15f, 0.9f, new Vector3(2.6f, 1.8f, 2.6f), 1.6f, 0.6f, 0.12f, 0.5f);
            var sweep = Attack("Brute_Sweep", 22f, 40f, 7f, 0.8f, 0.15f, 0.7f, new Vector3(4.2f, 1.4f, 2.2f), 1.2f, 0.3f, 0.08f, 0.3f);

            var wolf = Enemy(WolfDataPath, bandit, e =>
            {
                e.DisplayName = "Grey Wolf";
                e.MaxHealth = 38f; e.MaxPoise = 18f;
                e.MoveSpeed = 5.2f; e.TurnSpeed = 720f;
                e.AggroRange = 13f; e.AttackRange = 2.2f; e.AttackCooldown = 1.3f;
                e.Attacks = new[] { bite };
                e.HoldDistance = 4f; e.GiveUpAfter = 4f; e.LeashRange = 26f;
                e.PackHunter = true; e.RetreatAfterAttack = 0.9f;
                e.PlateHeight = 1.3f;
                e.XpReward = 18; e.StandingReward = 1;
                e.Loot = wolfLoot;
                e.RespawnDelay = 45f;
            }, ref created);
            var shield = Enemy(ShieldDataPath, bandit, e =>
            {
                e.DisplayName = "Bandit Shieldbearer";
                e.MaxHealth = 80f; e.MaxPoise = 45f;
                e.MoveSpeed = 3.3f;
                e.Attacks = new[] { bash, banditSlash };
                e.Shield = true;
                e.XpReward = Mathf.RoundToInt(bandit.XpReward * 1.4f);
            }, ref created);
            var brute = Enemy(BruteDataPath, bandit, e =>
            {
                e.DisplayName = "Blighted Brute";
                e.MaxHealth = 260f; e.MaxPoise = 150f;
                e.MoveSpeed = 2.6f; e.TurnSpeed = 240f;
                e.AggroRange = 14f; e.AttackRange = 2.8f; e.AttackCooldown = 2f;
                e.Attacks = new[] { slam, sweep };
                e.HoldDistance = 4f; e.GiveUpAfter = 6f; e.LeashRange = 20f;
                e.SuperArmor = true;
                e.PlateHeight = 3.1f;
                e.NightDamageBonus = 0f; e.NightAggroBonus = 0f; e.NightSpeedBonus = 0f; // it only comes out at night anyway
                e.XpReward = 120; e.StandingReward = 8;
                e.Loot = bruteLoot;
                e.RespawnDelay = 0f; // NightPresence brings it back the next night
            }, ref created);

            string wolfSheet = SpriteVisualsSetup.CreateSheet("Wolf", WolfPalette);
            string shieldSheet = SpriteVisualsSetup.CreateSheet("BanditShield", ShieldPalette);
            string bruteSheet = SpriteVisualsSetup.CreateSheet("BlightBrute", BrutePalette);

            ItemIconsSetup.Run(); // icons for the new items
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            // ---- Place them ----
            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            wolf = AssetDatabase.LoadAssetAtPath<EnemyData>(WolfDataPath);
            shield = AssetDatabase.LoadAssetAtPath<EnemyData>(ShieldDataPath);
            brute = AssetDatabase.LoadAssetAtPath<EnemyData>(BruteDataPath);
            var template = GameObject.Find("Bandit_A");
            if (template == null)
            {
                Debug.LogError("[Setup] Bandit_A not found in World_Test. Run the Milestone 2 setup first.");
                return;
            }
            Physics.SyncTransforms();

            int placed = 0;
            for (int i = 0; i < WolfNames.Length; i++)
            {
                var offset = Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0f, 0f, 2.2f);
                if (Place(template, WolfNames[i], WolfDen + offset, wolf, wolfSheet, 1f, out _)) placed++;
            }
            if (Place(template, ShieldName, ShieldSpot, shield, shieldSheet, 1f, out _)) placed++;
            if (Place(template, BruteName, BruteSpot, brute, bruteSheet, 1.4f, out var bruteObject)) placed++;
            if (!bruteObject.TryGetComponent(out NightPresence _)) bruteObject.AddComponent<NightPresence>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 40 setup complete: {created} new asset(s); {placed} new enemies placed " +
                      $"(wolf pack in the north-west woods, a shieldbearer at the bandit camp, a night-only Blighted Brute past it). " +
                      "New recipes: Fur Leggings (smithing), Ichor Draught (alchemy).");
        }

        /// <summary>Adds (or re-links) one enemy cloned from the Bandit template, on clear ground near 'spot'.</summary>
        static bool Place(GameObject template, string name, Vector3 spot, EnemyData data, string sheetPath, float visualScale, out GameObject enemy)
        {
            enemy = GameObject.Find(name);
            bool created = false;
            if (enemy == null)
            {
                enemy = Object.Instantiate(template, template.transform.parent);
                enemy.name = name;
                var ground = ClearGround(spot);
                var toCentre = new Vector3(-ground.x, 0f, -ground.z);
                enemy.transform.SetPositionAndRotation(ground + Vector3.up * 1.1f,
                    toCentre.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCentre) : Quaternion.identity);
                created = true;
            }
            var so = new SerializedObject(enemy.GetComponent<EnemyController>());
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            var visual = enemy.GetComponentInChildren<DirectionalSpriteRenderer>();
            if (visual != null)
            {
                var vso = new SerializedObject(visual);
                vso.FindProperty("sheet").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(sheetPath);
                vso.ApplyModifiedPropertiesWithoutUndo();
                visual.transform.localScale = Vector3.one * visualScale;
            }
            return created;
        }

        /// <summary>The nearest spot to 'near' on open, level ground (nothing within a metre and a half).</summary>
        static Vector3 ClearGround(Vector3 near)
        {
            for (float radius = 0f; radius <= 8f; radius += 1f)
                for (int step = 0; step < (radius == 0f ? 1 : 12); step++)
                {
                    var p = near + Quaternion.Euler(0f, step * 30f, 0f) * new Vector3(0f, 0f, radius);
                    if (!Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Mathf.Abs(hit.point.y) > 0.3f) continue; // on top of something
                    if (Physics.CheckSphere(hit.point + Vector3.up * 1.8f, 1.5f, ~0, QueryTriggerInteraction.Ignore)) continue; // clear of the ground itself
                    return hit.point;
                }
            return near;
        }

        static ItemData Material(string asset, string displayName, string description, Color32 color, int value, ref int created) =>
            Asset<ItemData>($"{ItemFolder}/{asset}.asset", i =>
            {
                i.DisplayName = displayName;
                i.Description = description;
                i.Category = ItemCategory.Material;
                i.PlaceholderColor = color;
                i.MaxStack = 99;
                i.BaseValue = value;
            }, ref created);

        static AttackData Attack(string name, float damage, float poise, float knockback, float startup, float active, float recovery,
            Vector3 hitbox, float centerZ, float lunge, float hitStop, float shake)
        {
            int unused = 0;
            return Asset<AttackData>($"{CombatRoot}/Attacks/{name}.asset", a =>
            {
                a.Damage = damage;
                a.PoiseDamage = poise;
                a.Knockback = knockback;
                a.Startup = startup;
                a.Active = active;
                a.Recovery = recovery;
                a.HitboxCenter = new Vector3(0f, 0f, centerZ);
                a.HitboxSize = hitbox;
                a.Lunge = lunge;
                a.HitStop = hitStop;
                a.CameraShake = shake;
            }, ref unused);
        }

        /// <summary>A copy of the Bandit's data (night settings, rewards) with these changes. Kept if it exists.</summary>
        static EnemyData Enemy(string path, EnemyData bandit, System.Action<EnemyData> configure, ref int created)
        {
            var existing = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (existing != null) return existing;
            var data = ScriptableObject.CreateInstance<EnemyData>();
            EditorUtility.CopySerialized(bandit, data);
            data.name = Path.GetFileNameWithoutExtension(path);
            var so = new SerializedObject(data);
            so.FindProperty("id").stringValue = ""; // a fresh ID from the database rebuild
            so.ApplyModifiedPropertiesWithoutUndo();
            configure(data);
            AssetDatabase.CreateAsset(data, path);
            created++;
            return data;
        }

        static RecipeData Recipe(string asset, CraftKind kind, ItemData output, params (ItemData item, int count)[] inputs)
        {
            int unused = 0;
            return Asset<RecipeData>($"{RecipeFolder}/{asset}.asset", r =>
            {
                r.Kind = kind;
                r.Output = output;
                r.OutputCount = 1;
                r.Inputs = new RecipeData.Ingredient[inputs.Length];
                for (int i = 0; i < inputs.Length; i++) r.Inputs[i] = new RecipeData.Ingredient { Item = inputs[i].item, Count = inputs[i].count };
            }, ref unused);
        }

        /// <summary>Loads the asset, or creates it with these defaults. Existing assets keep your tuning.</summary>
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
