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
    /// Milestone 7 setup: Disciplines (progression config), Knight combat skills + farming perks, weapons & armor,
    /// the blacksmith (Brenna's Forge), and progression/equipment/skills components on the player.
    /// Safe to re-run; existing assets keep your tuning.
    /// </summary>
    public static class ProgressionSetup
    {
        const string Root = "Assets/_Project";
        const string ProgressionFolder = Root + "/Data/Progression";
        const string SkillsFolder = Root + "/Data/Progression/Skills";
        const string AttacksFolder = Root + "/Data/Combat/Attacks";
        const string EquipmentFolder = Root + "/Data/Items/Equipment";
        const string EconomyFolder = Root + "/Data/Economy";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string SpriteMaterialPath = Root + "/Art/Materials/Sprite_Lit.mat";

        [MenuItem("Beast/Setup/Run Milestone 7 Setup (Progression)", priority = 6)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var knight = AssetDatabase.LoadAssetAtPath<ClassData>(Root + "/Data/Combat/Classes/Class_Knight.asset");
            var swordShield = AssetDatabase.LoadAssetAtPath<MovesetData>(Root + "/Data/Combat/Movesets/Knight_SwordShield.asset");
            var greatsword = AssetDatabase.LoadAssetAtPath<MovesetData>(Root + "/Data/Combat/Movesets/Knight_Greatsword.asset");
            int npcLayer = LayerMask.NameToLayer("NPC");
            if (knight == null || swordShield == null || greatsword == null || npcLayer < 0 ||
                !System.IO.File.Exists(TestWorldScenePath) || AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath) == null)
            {
                Debug.LogError("[Setup] Run the Milestone 1–6 setups first (Knight data, NPC layer, sprites and World_Test are required).");
                return;
            }

            foreach (var folder in new[] { ProgressionFolder, SkillsFolder, EquipmentFolder, EconomyFolder }) EnsureFolder(folder);

            var config = CreateConfig();
            CreateSkills(knight, swordShield, greatsword);
            var gear = CreateEquipment(swordShield, greatsword);
            var forge = CreateForge(gear);
            SetXpRewards();

            string configPath = AssetDatabase.GetAssetPath(config);
            string forgePath = AssetDatabase.GetAssetPath(forge);
            string rustedSwordPath = AssetDatabase.GetAssetPath(gear.RustedSwordShield);
            string rustedGreatswordPath = AssetDatabase.GetAssetPath(gear.RustedGreatsword);
            string gambesonPath = AssetDatabase.GetAssetPath(gear.Gambeson);
            string sheetPath = SpriteVisualsSetup.CreateSheet("Blacksmith", new PlaceholderSpriteGenerator.Palette
            {
                Body = new Color32(92, 64, 48, 255), Trim = new Color32(150, 150, 155, 255), Skin = new Color32(214, 168, 130, 255),
                Hair = new Color32(30, 26, 24, 255), Weapon = new Color32(120, 120, 125, 255), Outline = new Color32(20, 16, 14, 255),
                HasWeapon = true,
            });

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            config = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(configPath);
            forge = AssetDatabase.LoadAssetAtPath<ShopData>(forgePath);
            var rustedSword = AssetDatabase.LoadAssetAtPath<WeaponData>(rustedSwordPath);
            var rustedGreatsword = AssetDatabase.LoadAssetAtPath<WeaponData>(rustedGreatswordPath);
            var gambeson = AssetDatabase.LoadAssetAtPath<EquipmentData>(gambesonPath);
            var sheet = AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(sheetPath);
            var spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>(SpriteMaterialPath);

            SetUpPlayer(config, rustedSword, rustedGreatsword, gambeson);
            CreateShopNpc("Blacksmith", forge, sheet, spriteMaterial, npcLayer, new Vector3(2.5f, 1f, 4.5f), 180f);
            if (Object.FindFirstObjectByType<CharacterScreen>() == null)
                new GameObject("CharacterUI").AddComponent<CharacterScreen>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Setup] Milestone 7 progression setup complete. C opens the character screen; F6/F7 grant test XP.");
        }

        // ---------- Progression config ----------

        static ProgressionConfig CreateConfig() => Asset<ProgressionConfig>(ProgressionFolder, "ProgressionConfig", c =>
        {
            c.LevelCap = 30;
            c.BaseXp = 100f;
            c.Exponent = 1.5f;
            c.PointsPerLevel = 1;
            c.Growth = new[]
            {
                new ProgressionConfig.DisciplineGrowth
                {
                    Discipline = Discipline.Combat,
                    PerLevel = new[]
                    {
                        new StatModifier(StatType.MaxHealth, 8f),
                        new StatModifier(StatType.MaxPoise, 2f),
                        new StatModifier(StatType.MaxStamina, 2f),
                    },
                },
                new ProgressionConfig.DisciplineGrowth
                {
                    Discipline = Discipline.Farming,
                    PerLevel = new[] { new StatModifier(StatType.ExtraYieldChance, 0.01f) },
                },
            };
        });

        // ---------- Skills ----------

        static void CreateSkills(ClassData knight, MovesetData swordShield, MovesetData greatsword)
        {
            var warCryAttack = Attack("Skill_WarCry", 5, 30, 7f, 0.15f, 0.15f, 0.3f, Vector3.zero, new Vector3(5f, 2f, 5f), 0f, 0.06f, 0.35f);
            var chargeAttack = Attack("Skill_ShieldCharge", 18, 55, 9f, 0.12f, 0.35f, 0.35f, new Vector3(0f, 0f, 1f), new Vector3(1.8f, 1.6f, 1.6f), 5f, 0.08f, 0.3f);
            var whirlwindAttack = Attack("Skill_Whirlwind", 38, 45, 6f, 0.25f, 0.35f, 0.45f, Vector3.zero, new Vector3(5f, 1.6f, 5f), 0.5f, 0.1f, 0.35f);

            // Combat (Knight)
            var warCry = Skill("Skill_Knight_WarCry", s =>
            {
                Base(s, "War Cry", "A battle shout that shoves nearby enemies back and steels your nerve.", SkillKind.Active, Discipline.Combat, knight, 1, 2);
                s.Cooldown = 18f;
                s.Attack = warCryAttack;
                s.Buff = new[] { new StatModifier(StatType.AttackPower, 0f, 0.2f), new StatModifier(StatType.MaxPoise, 0f, 0.3f) };
                s.BuffDuration = 10f;
            });
            Skill("Skill_Knight_VeteransVigor", s =>
            {
                Base(s, "Veteran's Vigor", "Years of marching hardened your body.", SkillKind.Passive, Discipline.Combat, knight, 1, 1);
                s.PassiveModifiers = new[] { new StatModifier(StatType.MaxHealth, 0f, 0.1f) };
            });
            Skill("Skill_Knight_ShieldCharge", s =>
            {
                Base(s, "Shield Charge", "Dash forward behind your shield, bowling over whoever is in the way.", SkillKind.Active, Discipline.Combat, knight, 2, 3);
                s.Cooldown = 8f;
                s.RequiredMoveset = swordShield;
                s.Attack = chargeAttack;
                s.Prerequisites = new[] { warCry };
            });
            Skill("Skill_Knight_SecondWind", s =>
            {
                Base(s, "Second Wind", "A perfect parry gives you a moment to breathe.", SkillKind.Passive, Discipline.Combat, knight, 2, 3);
                s.PassiveModifiers = new[] { new StatModifier(StatType.ParryStaminaRestore, 20f) };
            });
            Skill("Skill_Knight_Whirlwind", s =>
            {
                Base(s, "Whirlwind", "Spin the greatsword in a wide circle, hitting everything around you.", SkillKind.Active, Discipline.Combat, knight, 3, 4);
                s.Cooldown = 10f;
                s.RequiredMoveset = greatsword;
                s.Attack = whirlwindAttack;
                s.Prerequisites = new[] { warCry };
            });
            Skill("Skill_Knight_IronWill", s =>
            {
                Base(s, "Iron Will", "You don't flinch. Much harder to stagger.", SkillKind.Passive, Discipline.Combat, knight, 3, 4);
                s.PassiveModifiers = new[] { new StatModifier(StatType.MaxPoise, 0f, 0.2f), new StatModifier(StatType.PoiseDamage, 15f) };
            });

            // Farming (any class)
            var greenThumb = Skill("Skill_Farming_GreenThumb", s =>
            {
                Base(s, "Green Thumb", "Your crops sometimes give a little extra.", SkillKind.Passive, Discipline.Farming, null, 1, 2);
                s.PassiveModifiers = new[] { new StatModifier(StatType.ExtraYieldChance, 0.2f) };
            });
            Skill("Skill_Farming_DeepRoots", s =>
            {
                Base(s, "Deep Roots", "Your crops survive one more dry day before wilting or dying.", SkillKind.Passive, Discipline.Farming, null, 2, 3);
                s.PassiveModifiers = new[] { new StatModifier(StatType.DroughtTolerance, 1f) };
            });
            Skill("Skill_Farming_SeedKeeper", s =>
            {
                Base(s, "Seed Keeper", "Careful sowing: planting sometimes doesn't use up the seed.", SkillKind.Passive, Discipline.Farming, null, 2, 3);
                s.PassiveModifiers = new[] { new StatModifier(StatType.SeedSaveChance, 0.15f) };
                s.Prerequisites = new[] { greenThumb };
            });
        }

        static void Base(SkillData s, string name, string description, SkillKind kind, Discipline discipline, ClassData forClass, int tier, int level)
        {
            s.DisplayName = name;
            s.Description = description;
            s.Kind = kind;
            s.Discipline = discipline;
            s.Class = forClass;
            s.Tier = tier;
            s.RequiredLevel = level;
            s.Cost = 1;
        }

        static SkillData Skill(string name, Action<SkillData> configure) => Asset(SkillsFolder, name, configure);

        static AttackData Attack(string name, float damage, float poise, float knockback, float startup, float active, float recovery,
            Vector3 center, Vector3 size, float lunge, float hitStop, float shake) => Asset<AttackData>(AttacksFolder, name, a =>
        {
            a.Damage = damage;
            a.PoiseDamage = poise;
            a.Knockback = knockback;
            a.Startup = startup;
            a.Active = active;
            a.Recovery = recovery;
            a.DodgeCancelAfter = startup + active;
            a.HitboxCenter = center;
            a.HitboxSize = size;
            a.Lunge = lunge;
            a.HitStop = hitStop;
            a.CameraShake = shake;
        });

        // ---------- Equipment ----------

        sealed class Gear
        {
            public WeaponData RustedSwordShield, IronSwordShield, RustedGreatsword, IronGreatsword;
            public EquipmentData PaddedCap, IronHelm, Gambeson, ChainHauberk, LeatherLeggings, SoldiersToken;
        }

        static Gear CreateEquipment(MovesetData swordShield, MovesetData greatsword) => new()
        {
            RustedSwordShield = Weapon("Weapon_RustedSwordShield", "Rusted Sword & Shield", "Battered army issue. Still better than nothing.", swordShield, 20,
                new StatModifier(StatType.AttackPower, 0f)),
            IronSwordShield = Weapon("Weapon_IronSwordShield", "Iron Sword & Shield", "Honest iron from Brenna's forge.", swordShield, 140,
                new StatModifier(StatType.AttackPower, 15f), new StatModifier(StatType.Defense, 5f)),
            RustedGreatsword = Weapon("Weapon_RustedGreatsword", "Rusted Greatsword", "Heavy, notched, and still terrifying.", greatsword, 25,
                new StatModifier(StatType.AttackPower, 0f)),
            IronGreatsword = Weapon("Weapon_IronGreatsword", "Iron Greatsword", "A proper two-hander. Splits shields.", greatsword, 170,
                new StatModifier(StatType.AttackPower, 18f), new StatModifier(StatType.PoiseDamage, 10f)),
            PaddedCap = Armor("Armor_PaddedCap", "Padded Cap", EquipSlot.Head, "Quilted cloth. Takes the edge off.", 25, new StatModifier(StatType.Defense, 4f)),
            IronHelm = Armor("Armor_IronHelm", "Iron Helm", EquipSlot.Head, "Dented but solid.", 110, new StatModifier(StatType.Defense, 10f), new StatModifier(StatType.MaxPoise, 5f)),
            Gambeson = Armor("Armor_Gambeson", "Gambeson", EquipSlot.Body, "Thick padded jacket every soldier wore.", 60, new StatModifier(StatType.Defense, 10f), new StatModifier(StatType.MaxPoise, 5f)),
            ChainHauberk = Armor("Armor_ChainHauberk", "Chain Hauberk", EquipSlot.Body, "Heavy mail. Worth every coin.", 220, new StatModifier(StatType.Defense, 22f), new StatModifier(StatType.MaxPoise, 10f)),
            LeatherLeggings = Armor("Armor_LeatherLeggings", "Leather Leggings", EquipSlot.Legs, "Supple and quiet.", 35, new StatModifier(StatType.Defense, 5f)),
            SoldiersToken = Armor("Armor_SoldiersToken", "Soldier's Token", EquipSlot.Accessory, "A dead comrade's tag. You fight a little longer for them.", 90, new StatModifier(StatType.MaxStamina, 15f)),
        };

        static WeaponData Weapon(string file, string name, string description, MovesetData moveset, int value, params StatModifier[] modifiers) =>
            Asset<WeaponData>(EquipmentFolder, file, w =>
            {
                ConfigureGear(w, name, description, EquipSlot.Weapon, value, modifiers, new Color(0.75f, 0.75f, 0.8f));
                w.Moveset = moveset;
            });

        static EquipmentData Armor(string file, string name, EquipSlot slot, string description, int value, params StatModifier[] modifiers) =>
            Asset<EquipmentData>(EquipmentFolder, file, a =>
                ConfigureGear(a, name, description, slot, value, modifiers, new Color(0.55f, 0.45f, 0.35f)));

        static void ConfigureGear(EquipmentData item, string name, string description, EquipSlot slot, int value, StatModifier[] modifiers, Color color)
        {
            item.DisplayName = name;
            item.Description = description;
            item.Category = ItemCategory.Equipment;
            item.MaxStack = 1;
            item.BaseValue = value;
            item.Slot = slot;
            item.Modifiers = modifiers;
            item.PlaceholderColor = color;
        }

        // ---------- Blacksmith ----------

        static ShopData CreateForge(Gear gear) => Asset<ShopData>(EconomyFolder, "Shop_Blacksmith", s =>
        {
            s.MerchantName = "Brenna's Forge";
            s.Greeting = "Steel doesn't care whose side you fought on. Neither do I. Bring me scrap and I'll pay for it.";
            s.Stock = new[]
            {
                Stock(gear.IronSwordShield, 1), Stock(gear.IronGreatsword, 1), Stock(gear.IronHelm, 1),
                Stock(gear.ChainHauberk, 1), Stock(gear.PaddedCap, 2), Stock(gear.LeatherLeggings, 2), Stock(gear.SoldiersToken, 1),
            };
            s.Buys = new[] { ItemCategory.Material, ItemCategory.Equipment };
        });

        static ShopData.StockEntry Stock(ItemData item, int daily) => new() { Item = item, DailyStock = daily };

        static void CreateShopNpc(string objectName, ShopData shop, DirectionalSpriteSheet sheet, Material material, int layer, Vector3 position, float yaw)
        {
            if (GameObject.Find(objectName) != null)
            {
                Debug.Log($"[Setup] {objectName} already exists in World_Test; kept as-is.");
                return;
            }

            var npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            npc.name = objectName;
            npc.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            npc.layer = layer;
            npc.AddComponent<NpcController>();
            var shopkeeper = npc.AddComponent<Shopkeeper>();
            var so = new SerializedObject(shopkeeper);
            so.FindProperty("shop").objectReferenceValue = shop;
            so.ApplyModifiedPropertiesWithoutUndo();
            SpriteVisualsSetup.AddVisual(npc, sheet, material);
        }

        // ---------- XP rewards ----------

        static void SetXpRewards()
        {
            SetEnemyXp("Enemy_Bandit", 25);
            SetEnemyXp("Enemy_TrainingDummy", 2);
            SetCropXp("Crop_Turnip", 6);
            SetCropXp("Crop_Healroot", 8);
        }

        /// <summary>Only replaces the untouched default (10), so hand-tuned values survive re-runs.</summary>
        static void SetEnemyXp(string file, int xp)
        {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyData>($"{Root}/Data/Combat/Enemies/{file}.asset");
            if (enemy == null || enemy.XpReward != 10) return;
            enemy.XpReward = xp;
            EditorUtility.SetDirty(enemy);
        }

        static void SetCropXp(string file, int xp)
        {
            var crop = AssetDatabase.LoadAssetAtPath<CropData>($"{Root}/Data/Farming/{file}.asset");
            if (crop == null || crop.HarvestXp != 5) return;
            crop.HarvestXp = xp;
            EditorUtility.SetDirty(crop);
        }

        // ---------- Player ----------

        static void SetUpPlayer(ProgressionConfig config, WeaponData mainWeapon, WeaponData secondWeapon, EquipmentData body)
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No object tagged Player in World_Test.");
                return;
            }

            GetOrAdd<PlayerStats>(player);
            var progression = GetOrAdd<PlayerProgression>(player);
            var so = new SerializedObject(progression);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!player.TryGetComponent(out PlayerEquipment _))
            {
                var equipment = player.AddComponent<PlayerEquipment>();
                var eso = new SerializedObject(equipment);
                eso.FindProperty("startingMainWeapon").objectReferenceValue = mainWeapon;
                eso.FindProperty("startingSecondWeapon").objectReferenceValue = secondWeapon;
                var armor = eso.FindProperty("startingArmor");
                armor.arraySize = 1;
                armor.GetArrayElementAtIndex(0).objectReferenceValue = body;
                eso.ApplyModifiedPropertiesWithoutUndo();
            }
            GetOrAdd<PlayerSkills>(player);
        }

        // ---------- Helpers ----------

        static T GetOrAdd<T>(GameObject go) where T : Component =>
            go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();

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
