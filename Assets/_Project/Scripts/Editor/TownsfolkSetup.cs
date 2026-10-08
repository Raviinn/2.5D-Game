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
    /// Milestone 43 setup (more townsfolk):
    /// - Maren, the healer: tends your wounds for free, sells remedies, gives "Herbs for Maren";
    /// - Tobin, her nephew, a farmer without a farm: farming tips, gives "Wolves at the Fold";
    /// - Captain Hale of the watch, who works nights: gives "Shield Wall", then "The Thing in the Dead Wood".
    /// Each has a daily routine (places under [NPC Places]); Maren and Tobin share the house south of the field, Hale
    /// sleeps in the tower by day. Needs Milestone 40 (wolves, shieldbearer, brute) for the quests.
    /// Safe to re-run: existing assets, NPCs and routines are kept.
    /// </summary>
    public static class TownsfolkSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string SpeakersFolder = Root + "/Data/Dialogue/Speakers";
        const string QuestsFolder = Root + "/Data/Quests";
        const string ShopFolder = Root + "/Data/Economy";

        public const string HealerName = "Healer";
        public const string FarmerName = "Farmer";
        public const string GuardName = "Guard";

        [MenuItem("Beast/Setup/Run Milestone 43 Setup (More Townsfolk)", priority = 35)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] World_Test not found. Run the earlier setups first.");
                return;
            }
            T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>($"{Root}/Data/{path}.asset");
            var wolf = Load<EnemyData>("Combat/Enemies/Enemy_Wolf");
            var shieldbearer = Load<EnemyData>("Combat/Enemies/Enemy_BanditShield");
            var brute = Load<EnemyData>("Combat/Enemies/Enemy_BlightBrute");
            var healroot = Load<ItemData>("Items/Item_Healroot");
            var draught = Load<ItemData>("Items/Item_HealingDraught");
            var tonic = Load<ItemData>("Items/Item_StaminaTonic");
            var bread = Load<ItemData>("Items/Item_Bread");
            var healrootSeeds = Load<ItemData>("Items/Item_HealrootSeeds");
            var turnipSeeds = Load<ItemData>("Items/Item_TurnipSeeds");
            if (wolf == null || shieldbearer == null || brute == null || healroot == null || draught == null || tonic == null ||
                bread == null || healrootSeeds == null || turnipSeeds == null)
            {
                Debug.LogError("[Setup] Missing enemies or items. Run the Milestone 26 and 40 setups first.");
                return;
            }
            if (!DialogueQuestSetup.CompileInk()) return;

            // ---- Data ----
            var maren = Asset<SpeakerData>($"{SpeakersFolder}/Speaker_Maren.asset", s => { s.DisplayName = "Maren"; s.PlaceholderColor = new Color(0.36f, 0.48f, 0.42f); });
            var tobin = Asset<SpeakerData>($"{SpeakersFolder}/Speaker_Tobin.asset", s => { s.DisplayName = "Tobin"; s.PlaceholderColor = new Color(0.55f, 0.42f, 0.24f); });
            var hale = Asset<SpeakerData>($"{SpeakersFolder}/Speaker_Hale.asset", s => { s.DisplayName = "Hale"; s.PlaceholderColor = new Color(0.32f, 0.38f, 0.5f); });

            var herbs = Quest("Quest_HerbsForMaren", q =>
            {
                Base(q, "Herbs for Maren", "The Blight took every wild Healroot patch. Maren the healer needs three sprigs from your field.", "Maren");
                q.Objectives = new[] { new QuestData.Objective { Type = ObjectiveType.Collect, Item = healroot, Count = 3 } };
                q.GoldReward = 35;
                q.ItemRewards = new[] { new QuestData.ItemReward { Item = draught, Count = 2 } };
                q.FarmingXp = 25;
            });
            var wolves = Quest("Quest_WolvesAtTheFold", q =>
            {
                Base(q, "Wolves at the Fold", "A wolf pack dens in the north-west woods and took Maren's last goats. Tobin wants it gone.", "Tobin");
                q.Objectives = new[] { new QuestData.Objective { Type = ObjectiveType.Kill, Enemy = wolf, Count = 3 } };
                q.GoldReward = 45;
                q.CombatXp = 50;
                q.ItemRewards = new[] { new QuestData.ItemReward { Item = turnipSeeds, Count = 6 } };
            });
            var shieldWall = Quest("Quest_ShieldWall", q =>
            {
                Base(q, "Shield Wall", "A deserter with a tower shield holds the bandit camp together. Captain Hale wants him down. Side or back, never the front.", "Hale");
                q.Objectives = new[] { new QuestData.Objective { Type = ObjectiveType.Kill, Enemy = shieldbearer, Count = 1 } };
                q.GoldReward = 60;
                q.CombatXp = 50;
            });
            var deadWood = Quest("Quest_DeadWood", q =>
            {
                Base(q, "The Thing in the Dead Wood", "Something huge walks the blighted wood past the bandit camp, only at night. Hale asks you to hunt it.", "Hale");
                q.Prerequisites = new[] { shieldWall };
                q.Objectives = new[] { new QuestData.Objective { Type = ObjectiveType.Kill, Enemy = brute, Count = 1 } };
                q.GoldReward = 150;
                q.CombatXp = 150;
            });
            var shop = Asset<ShopData>($"{ShopFolder}/Shop_Maren.asset", s =>
            {
                s.MerchantName = "Maren's Remedies";
                s.Greeting = "Remedies, mostly. Don't drink them all at once.";
                s.Stock = new[]
                {
                    new ShopData.StockEntry { Item = draught, DailyStock = 6 },
                    new ShopData.StockEntry { Item = tonic, DailyStock = 4 },
                    new ShopData.StockEntry { Item = healrootSeeds, DailyStock = 4 },
                    new ShopData.StockEntry { Item = bread, DailyStock = 5 },
                };
                s.Buys = new[] { ItemCategory.Crop, ItemCategory.Material, ItemCategory.Consumable };
            });

            string marenSheet = SpriteVisualsSetup.CreateSheet("Maren", new PlaceholderSpriteGenerator.Palette
            {
                Body = new Color32(92, 112, 96, 255), Trim = new Color32(70, 58, 46, 255), Skin = new Color32(214, 176, 146, 255),
                Hair = new Color32(196, 192, 184, 255), Outline = new Color32(26, 22, 20, 255),
            });
            string tobinSheet = SpriteVisualsSetup.CreateSheet("Tobin", new PlaceholderSpriteGenerator.Palette
            {
                Body = new Color32(150, 116, 66, 255), Trim = new Color32(86, 62, 40, 255), Skin = new Color32(222, 172, 128, 255),
                Hair = new Color32(176, 120, 52, 255), Outline = new Color32(28, 20, 14, 255),
            });
            string haleSheet = SpriteVisualsSetup.CreateSheet("Hale", new PlaceholderSpriteGenerator.Palette
            {
                Body = new Color32(74, 88, 112, 255), Trim = new Color32(150, 150, 156, 255), Skin = new Color32(190, 140, 104, 255),
                Hair = new Color32(70, 64, 60, 255), Weapon = new Color32(200, 200, 196, 255), Outline = new Color32(18, 18, 22, 255),
                HasWeapon = true, HasShield = true,
            });

            var paths = new Dictionary<string, string>();
            foreach (Object asset in new Object[] { maren, tobin, hale, herbs, wolves, shieldWall, deadWood, shop })
                paths[asset.name] = AssetDatabase.GetAssetPath(asset);
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            // ---- Scene ----
            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            T Reload<T>(string name) where T : Object => AssetDatabase.LoadAssetAtPath<T>(paths[name]);
            var template = GameObject.Find("Merchant");
            if (template == null)
            {
                Debug.LogError("[Setup] Merchant not found. Run the Milestone 6 setup first.");
                return;
            }
            Physics.SyncTransforms();
            var places = GameObject.Find(NpcScheduleSetup.PlacesName);
            if (places == null) places = new GameObject(NpcScheduleSetup.PlacesName);
            var root = places.transform;

            var well = GameObject.Find("Well");
            Vector3 wellCentre = well != null ? well.transform.position : new Vector3(4f, 0f, 6f);
            var report = new List<string>();

            // Maren: herb table by the square by day, herbs along the field fence in the afternoon, home at night.
            var healer = Npc(template, HealerName, Reload<SpeakerData>("Speaker_Maren"), "maren", AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(marenSheet),
                Reload<ShopData>("Shop_Maren"), new[] { Reload<QuestData>("Quest_HerbsForMaren") }, out bool newHealer);
            if (newHealer || !healer.TryGetComponent(out NpcSchedule _))
            {
                var table = NpcScheduleSetup.Place(root, "Maren_Table", NpcScheduleSetup.ClearSpotNear(new Vector3(-1f, 0f, 7f), 1.2f, 2), Vector3.back);
                var herbsSpot = NpcScheduleSetup.Place(root, "Maren_Herbs", NpcScheduleSetup.ClearSpotNear(new Vector3(8f, 0f, 0.9f), 0.6f, 0), Vector3.back);
                var home = HomeAt(root, "Maren_Home", "House_C", -0.6f);
                healer.transform.SetPositionAndRotation(table.position + Vector3.up, table.rotation);
                NpcScheduleSetup.AddSchedule(healer,
                    (6.5f, table, "At her herb table", false),
                    (15f, herbsSpot, "Gathering herbs by the field", false),
                    (17.5f, table, "At her herb table", false),
                    (21f, home, "Asleep at home", true));
                report.Add("Maren");
            }

            // Tobin: mends the fence, watches the far fields, lunches at the well, home after dark.
            var farmer = Npc(template, FarmerName, Reload<SpeakerData>("Speaker_Tobin"), "tobin", AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(tobinSheet),
                null, new[] { Reload<QuestData>("Quest_WolvesAtTheFold") }, out bool newFarmer);
            if (newFarmer || !farmer.TryGetComponent(out NpcSchedule _))
            {
                var fence = NpcScheduleSetup.Place(root, "Tobin_Fence", NpcScheduleSetup.ClearSpotNear(new Vector3(2.3f, 0f, -4f), 0.6f, 6), Vector3.right);
                var meadow = NpcScheduleSetup.Place(root, "Tobin_Meadow", NpcScheduleSetup.ClearSpotNear(new Vector3(9.5f, 0f, -10.5f), 1f, 0), Vector3.back);
                var lunch = NpcScheduleSetup.Place(root, "Tobin_Well", NpcScheduleSetup.ClearSpotNear(wellCentre, 1.7f, 2), Vector3.forward);
                NpcScheduleSetup.FaceTowards(lunch, wellCentre);
                var home = HomeAt(root, "Tobin_Home", "House_C", 0.6f);
                farmer.transform.SetPositionAndRotation(fence.position + Vector3.up, fence.rotation);
                NpcScheduleSetup.AddSchedule(farmer,
                    (6f, fence, "Mending the fence", false),
                    (10f, meadow, "Watching the far meadow", false),
                    (12.5f, lunch, "Lunch at the well", false),
                    (13.5f, fence, "Mending the fence", false),
                    (20.5f, home, "Asleep at home", true));
                report.Add("Tobin");
            }

            // Hale: keeps the night watch, walks the rounds, sleeps in the tower by day.
            var guard = Npc(template, GuardName, Reload<SpeakerData>("Speaker_Hale"), "hale", AssetDatabase.LoadAssetAtPath<DirectionalSpriteSheet>(haleSheet),
                null, new[] { Reload<QuestData>("Quest_ShieldWall"), Reload<QuestData>("Quest_DeadWood") }, out bool newGuard);
            if (newGuard || !guard.TryGetComponent(out NpcSchedule _))
            {
                var lookout = NpcScheduleSetup.Place(root, "Hale_Lookout", NpcScheduleSetup.ClearSpotNear(new Vector3(12f, 0f, 11f), 1.5f, 0), new Vector3(1f, 0f, 1f));
                var rounds = NpcScheduleSetup.Place(root, "Hale_Square", NpcScheduleSetup.ClearSpotNear(new Vector3(0f, 0f, 3f), 1.5f, 4), Vector3.forward);
                var road = NpcScheduleSetup.Place(root, "Hale_Road", NpcScheduleSetup.ClearSpotNear(new Vector3(7f, 0f, 11f), 1.5f, 1), new Vector3(1f, 0f, 1f));
                var tower = TowerDoor(root);
                guard.transform.SetPositionAndRotation(lookout.position + Vector3.up, lookout.rotation);
                NpcScheduleSetup.AddSchedule(guard,
                    (0f, lookout, "Night watch", false),
                    (3f, rounds, "Walking the rounds", false),
                    (5f, lookout, "Night watch", false),
                    (7f, tower, "Asleep in the tower", true),
                    (15f, road, "Keeping the road", false),
                    (19f, rounds, "Walking the rounds", false),
                    (21f, lookout, "Night watch", false));
                report.Add("Hale");
            }

            // The dialogue runner needs the new speakers (names, portraits).
            var runner = Object.FindFirstObjectByType<DialogueRunner>();
            if (runner != null)
            {
                var so = new SerializedObject(runner);
                var list = so.FindProperty("speakers");
                foreach (var speaker in new[] { Reload<SpeakerData>("Speaker_Maren"), Reload<SpeakerData>("Speaker_Tobin"), Reload<SpeakerData>("Speaker_Hale") })
                {
                    bool present = false;
                    for (int i = 0; i < list.arraySize && !present; i++) present = list.GetArrayElementAtIndex(i).objectReferenceValue == speaker;
                    if (present) continue;
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = speaker;
                }
                so.FindProperty("storyJson").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Dialogue/Main.json");
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 43 townsfolk setup complete: {(report.Count > 0 ? string.Join(", ", report) + " added with their routines" : "townsfolk kept")}. " +
                      "Maren (healer) by the square, Tobin (farmer) by your field, Captain Hale (watch) out at night. Four new side quests.");
        }

        /// <summary>A talkable NPC cloned from the merchant (collider, NPC layer, sprite), with its own speaker, knot and quests.</summary>
        static GameObject Npc(GameObject template, string name, SpeakerData speaker, string knot, DirectionalSpriteSheet sheet,
            ShopData shop, QuestData[] quests, out bool created)
        {
            var npc = GameObject.Find(name);
            created = npc == null;
            if (created)
            {
                npc = Object.Instantiate(template, template.transform.parent);
                npc.name = name;
                if (npc.TryGetComponent(out NpcSchedule copiedRoutine)) Object.DestroyImmediate(copiedRoutine);
                if (npc.TryGetComponent(out Shopkeeper copiedShop)) Object.DestroyImmediate(copiedShop);
            }

            if (shop != null)
            {
                if (!npc.TryGetComponent(out Shopkeeper shopkeeper)) shopkeeper = npc.AddComponent<Shopkeeper>();
                var so = new SerializedObject(shopkeeper);
                so.FindProperty("shop").objectReferenceValue = shop;
                so.FindProperty("directInteraction").boolValue = false; // opened from the conversation
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!npc.TryGetComponent(out DialogueSpeaker talker)) talker = npc.AddComponent<DialogueSpeaker>();
            var talk = new SerializedObject(talker);
            talk.FindProperty("speaker").objectReferenceValue = speaker;
            talk.FindProperty("knot").stringValue = knot;
            if (created)
            {
                SetArray(talk.FindProperty("questsOffered"), quests);
                SetArray(talk.FindProperty("questsTurnedIn"), quests);
            }
            talk.ApplyModifiedPropertiesWithoutUndo();

            var visual = npc.GetComponentInChildren<DirectionalSpriteRenderer>();
            if (visual != null)
            {
                var vso = new SerializedObject(visual);
                vso.FindProperty("sheet").objectReferenceValue = sheet;
                vso.ApplyModifiedPropertiesWithoutUndo();
            }
            return npc;
        }

        /// <summary>A spot just outside this house's door, shifted sideways (two people share a house).</summary>
        static Transform HomeAt(Transform root, string placeName, string houseName, float sideways)
        {
            var existing = root.Find(placeName);
            if (existing != null) return existing;
            var house = GameObject.Find(houseName);
            Transform door = null;
            if (house != null)
                foreach (var t in house.GetComponentsInChildren<Transform>())
                    if (t.name == "Door") { door = t; break; }
            if (door == null)
                return NpcScheduleSetup.Place(root, placeName, NpcScheduleSetup.Ground(house != null ? house.transform.position + Vector3.forward * 4f : Vector3.zero), Vector3.back);
            // A door decal's visible side faces -forward (EnvironmentSetup.Decal): stand there, facing the door.
            Vector3 outward = -door.forward;
            outward.y = 0f;
            outward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            return NpcScheduleSetup.Place(root, placeName, NpcScheduleSetup.Ground(door.position + outward * 0.9f + side * sideways), -outward);
        }

        /// <summary>The foot of the watch tower, on the side facing the village.</summary>
        static Transform TowerDoor(Transform root)
        {
            const string placeName = "Hale_Tower";
            var existing = root.Find(placeName);
            if (existing != null) return existing;
            var tower = GameObject.Find("Tower");
            if (tower == null) return NpcScheduleSetup.Place(root, placeName, NpcScheduleSetup.ClearSpotNear(new Vector3(-6f, 0f, -6f), 1f, 0), Vector3.forward);
            var bounds = new Bounds(tower.transform.position, Vector3.zero);
            foreach (var c in tower.GetComponentsInChildren<Collider>()) bounds.Encapsulate(c.bounds);
            Vector3 toVillage = new Vector3(2f, 0f, 3f) - bounds.center;
            toVillage.y = 0f;
            toVillage.Normalize();
            float reach = Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.9f;
            var spot = NpcScheduleSetup.ClearSpotNear(bounds.center + toVillage * reach, 0.5f, 0);
            return NpcScheduleSetup.Place(root, placeName, spot, -toVillage);
        }

        static QuestData Quest(string file, System.Action<QuestData> configure) => Asset($"{QuestsFolder}/{file}.asset", configure);

        static void Base(QuestData q, string title, string summary, string turnInAt)
        {
            q.Title = title;
            q.Summary = summary;
            q.Type = QuestType.Side;
            q.TurnInAt = turnInAt;
            q.TakeItemsOnTurnIn = true;
        }

        static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        /// <summary>Loads the asset, or creates it with these defaults. Existing assets keep your tuning.</summary>
        static T Asset<T>(string path, System.Action<T> configureNew) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            configureNew(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
