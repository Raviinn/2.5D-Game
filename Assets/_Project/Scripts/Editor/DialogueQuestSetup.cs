using System;
using System.Collections.Generic;
using System.IO;
using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 8 setup: compiles the Ink dialogue, creates speakers, story/side quests and contracts,
    /// and wires the dialogue runner, quest UI, NPC conversations and a contracts board into World_Test.
    /// Safe to re-run; existing assets keep your tuning and .ink files are never overwritten.
    /// </summary>
    public static class DialogueQuestSetup
    {
        const string Root = "Assets/_Project";
        const string DialogueFolder = Root + "/Dialogue";
        const string StoryJsonPath = DialogueFolder + "/Main.json";
        const string SpeakersFolder = Root + "/Data/Dialogue/Speakers";
        const string QuestsFolder = Root + "/Data/Quests";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";

        [MenuItem("Beast/Setup/Run Milestone 8 Setup (Dialogue & Quests)", priority = 7)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Item("Item_Turnip", out var turnip);
            Item("Item_Healroot", out var healroot);
            Item("Item_HealrootSeeds", out var healrootSeeds);
            Item("Item_IronScrap", out var ironScrap);
            Item("Item_BanditCloth", out var cloth);
            var bandit = AssetDatabase.LoadAssetAtPath<EnemyData>(Root + "/Data/Combat/Enemies/Enemy_Bandit.asset");
            if (turnip == null || healroot == null || healrootSeeds == null || ironScrap == null || cloth == null || bandit == null ||
                !File.Exists(TestWorldScenePath))
            {
                Debug.LogError("[Setup] Run the Milestone 1–7 setups first (items, crops, enemies and both merchants are required).");
                return;
            }

            if (!CompileInk()) return;

            EnsureFolder(SpeakersFolder);
            EnsureFolder(QuestsFolder);

            var oswin = Speaker("Speaker_Oswin", "Oswin", new Color(0.24f, 0.44f, 0.28f));
            var brenna = Speaker("Speaker_Brenna", "Brenna", new Color(0.36f, 0.25f, 0.19f));

            var banditTrouble = Quest("Quest_BanditTrouble", q =>
            {
                Base(q, "Bandit Trouble", "Deserters raid anyone who still has food. Oswin wants three of them dealt with.", QuestType.Story, "Oswin");
                q.Objectives = new[] { Kill(bandit, 3) };
                q.GoldReward = 50;
                q.CombatXp = 60;
            });
            var roots = Quest("Quest_RootsOfTheBlight", q =>
            {
                Base(q, "Roots of the Blight", "Brenna found ash that moves. She'll only show someone who has bled for the town.", QuestType.Story, "Brenna");
                q.Prerequisites = new[] { banditTrouble };
                q.Objectives = new[]
                {
                    new QuestData.Objective { Type = ObjectiveType.Custom, Description = "Hear Brenna out at the forge" },
                    Collect(healroot, 1),
                };
                q.GoldReward = 30;
                q.CombatXp = 30;
                q.FarmingXp = 40;
            });
            var tasteOfHome = Quest("Quest_TasteOfHome", q =>
            {
                Base(q, "A Taste of Home", "The refugees are hungry. Oswin will pay above market for fresh turnips.", QuestType.Side, "Oswin");
                q.Objectives = new[] { Collect(turnip, 5) };
                q.GoldReward = 40;
                q.ItemRewards = new[] { new QuestData.ItemReward { Item = healrootSeeds, Count = 3 } };
                q.FarmingXp = 30;
            });
            var scrapRun = Quest("Quest_ScrapRun", q =>
            {
                Base(q, "Scrap Run", "Iron is scarce since the war. Brenna pays well for scrap.", QuestType.Side, "Brenna");
                q.Objectives = new[] { Collect(ironScrap, 4) };
                q.GoldReward = 60;
                q.CombatXp = 40;
            });
            var bounty = Quest("Contract_BanditBounty", q =>
            {
                Base(q, "Bounty: Road Bandits", "The Free Hollows pay for every three deserters driven off the road.", QuestType.Contract, "the Contracts Board");
                q.Objectives = new[] { Kill(bandit, 3) };
                q.GoldReward = 45;
                q.CombatXp = 40;
            });
            var supplyRun = Quest("Contract_SupplyRun", q =>
            {
                Base(q, "Supply Run: Turnips", "The refugee camp needs food. Deliver six turnips.", QuestType.Contract, "the Contracts Board");
                q.Objectives = new[] { Collect(turnip, 6) };
                q.GoldReward = 50;
                q.FarmingXp = 20;
            });
            var bandages = Quest("Contract_Bandages", q =>
            {
                Base(q, "Cloth for Bandages", "The healers need cloth. Even bandit rags will do.", QuestType.Contract, "the Contracts Board");
                q.Objectives = new[] { Collect(cloth, 5) };
                q.GoldReward = 25;
                q.CombatXp = 10;
            });

            var paths = new Dictionary<string, string>();
            foreach (Object asset in new Object[] { oswin, brenna, banditTrouble, roots, tasteOfHome, scrapRun, bounty, supplyRun, bandages })
                paths[asset.name] = AssetDatabase.GetAssetPath(asset);

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            T Load<T>(string name) where T : Object => AssetDatabase.LoadAssetAtPath<T>(paths[name]);
            var storyJson = AssetDatabase.LoadAssetAtPath<TextAsset>(StoryJsonPath);

            BuildDialogueObjects(storyJson, new[] { Load<SpeakerData>("Speaker_Oswin"), Load<SpeakerData>("Speaker_Brenna") });

            var player = GameObject.FindWithTag("Player");
            if (player != null && !player.TryGetComponent(out QuestLog _)) player.AddComponent<QuestLog>();

            SetUpNpc("Merchant", Load<SpeakerData>("Speaker_Oswin"), "oswin",
                offered: new[] { Load<QuestData>("Quest_BanditTrouble"), Load<QuestData>("Quest_TasteOfHome"), Load<QuestData>("Quest_RootsOfTheBlight") },
                turnedIn: new[] { Load<QuestData>("Quest_BanditTrouble"), Load<QuestData>("Quest_TasteOfHome") });
            SetUpNpc("Blacksmith", Load<SpeakerData>("Speaker_Brenna"), "brenna",
                offered: new[] { Load<QuestData>("Quest_ScrapRun") },
                turnedIn: new[] { Load<QuestData>("Quest_ScrapRun"), Load<QuestData>("Quest_RootsOfTheBlight") });

            BuildContractBoard(new[] { Load<QuestData>("Contract_BanditBounty"), Load<QuestData>("Contract_SupplyRun"), Load<QuestData>("Contract_Bandages") });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Setup] Milestone 8 dialogue & quests setup complete. Talk to Oswin (F). J: journal, T: switch tracked quest, M: map.");
        }

        // ---------- Ink ----------

        [MenuItem("Beast/Dialogue/Compile Ink Story")]
        static void CompileInkFromMenu()
        {
            if (CompileInk()) Debug.Log($"[Dialogue] Compiled {StoryJsonPath}.");
        }

        /// <summary>Compiles Dialogue/Main.ink (and its INCLUDEs) to Main.json with the Ink compiler. False on errors.</summary>
        internal static bool CompileInk()
        {
            string folder = Path.GetFullPath(DialogueFolder);
            string mainPath = Path.Combine(folder, "Main.ink");
            if (!File.Exists(mainPath))
            {
                Debug.LogError($"[Dialogue] {DialogueFolder}/Main.ink not found.");
                return false;
            }

            var errors = new List<string>();
            var options = new Ink.Compiler.Options
            {
                sourceFilename = "Main.ink",
                fileHandler = new FolderFileHandler(folder),
                errorHandler = (message, type) =>
                {
                    if (type == Ink.ErrorType.Error) errors.Add(message);
                    else Debug.LogWarning($"[Ink] {message}");
                },
            };
            var story = new Ink.Compiler(File.ReadAllText(mainPath), options).Compile();
            if (story == null || errors.Count > 0)
            {
                foreach (string error in errors) Debug.LogError($"[Ink] {error}");
                Debug.LogError("[Dialogue] Ink compile failed. Fix the errors above (file and line are in each message).");
                return false;
            }

            File.WriteAllText(Path.GetFullPath(StoryJsonPath), story.ToJson());
            AssetDatabase.ImportAsset(StoryJsonPath);
            return true;
        }

        sealed class FolderFileHandler : Ink.IFileHandler
        {
            readonly string root;
            public FolderFileHandler(string root) => this.root = root;
            public string ResolveInkFilename(string includeName) => Path.Combine(root, includeName);
            public string LoadInkFileContents(string fullFilename) => File.ReadAllText(fullFilename);
        }

        // ---------- Scene ----------

        static void BuildDialogueObjects(TextAsset storyJson, SpeakerData[] speakers)
        {
            var runner = Object.FindFirstObjectByType<DialogueRunner>();
            if (runner == null)
            {
                var go = new GameObject("Dialogue");
                runner = go.AddComponent<DialogueRunner>();
                go.AddComponent<DialogueBox>();
            }
            var so = new SerializedObject(runner);
            so.FindProperty("storyJson").objectReferenceValue = storyJson;
            var list = so.FindProperty("speakers");
            list.arraySize = speakers.Length;
            for (int i = 0; i < speakers.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = speakers[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var journal = Object.FindFirstObjectByType<QuestJournal>();
            var ui = journal != null ? journal.gameObject : new GameObject("QuestUI");
            if (!ui.TryGetComponent(out QuestJournal _)) ui.AddComponent<QuestJournal>();
            if (!ui.TryGetComponent(out ContractBoardScreen _)) ui.AddComponent<ContractBoardScreen>();
            if (!ui.TryGetComponent(out QuestTracker _)) ui.AddComponent<QuestTracker>();
            if (!ui.TryGetComponent(out Minimap _)) ui.AddComponent<Minimap>();
        }

        static void SetUpNpc(string objectName, SpeakerData speaker, string knot, QuestData[] offered, QuestData[] turnedIn)
        {
            var npc = GameObject.Find(objectName);
            if (npc == null)
            {
                Debug.LogWarning($"[Setup] {objectName} not found in World_Test; skipped.");
                return;
            }

            if (!npc.TryGetComponent(out DialogueSpeaker talker))
            {
                talker = npc.AddComponent<DialogueSpeaker>();
                var so = new SerializedObject(talker);
                so.FindProperty("speaker").objectReferenceValue = speaker;
                so.FindProperty("knot").stringValue = knot;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Merge quest markers: add any missing, keep anything you added by hand.
            var markers = new SerializedObject(talker);
            AddMissing(markers.FindProperty("questsOffered"), offered);
            AddMissing(markers.FindProperty("questsTurnedIn"), turnedIn);
            markers.ApplyModifiedPropertiesWithoutUndo();

            // The conversation opens the shop (open_shop), so the shop no longer has its own prompt.
            if (npc.TryGetComponent(out Shopkeeper shopkeeper))
            {
                var so = new SerializedObject(shopkeeper);
                so.FindProperty("directInteraction").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void BuildContractBoard(QuestData[] contracts)
        {
            if (Object.FindFirstObjectByType<ContractBoard>() != null)
            {
                Debug.Log("[Setup] Contracts board already exists in World_Test; kept as-is.");
                return;
            }

            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "ContractsBoard";
            board.transform.SetPositionAndRotation(new Vector3(-2.5f, 0.9f, 1.5f), Quaternion.Euler(0f, 30f, 0f));
            board.transform.localScale = new Vector3(1.4f, 1.8f, 0.2f);
            board.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMaterial("Greybox_Board", new Color(0.45f, 0.32f, 0.2f));
            var component = board.AddComponent<ContractBoard>();
            var so = new SerializedObject(component);
            SetArray(so.FindProperty("contracts"), contracts);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Data ----------

        static SpeakerData Speaker(string file, string displayName, Color color) => Asset<SpeakerData>(SpeakersFolder, file, s =>
        {
            s.DisplayName = displayName;
            s.PlaceholderColor = color;
        });

        static QuestData Quest(string file, Action<QuestData> configure) => Asset(QuestsFolder, file, configure);

        static void Base(QuestData q, string title, string summary, QuestType type, string turnInAt)
        {
            q.Title = title;
            q.Summary = summary;
            q.Type = type;
            q.TurnInAt = turnInAt;
            q.TakeItemsOnTurnIn = true;
        }

        static QuestData.Objective Kill(EnemyData enemy, int count) => new() { Type = ObjectiveType.Kill, Enemy = enemy, Count = count };
        static QuestData.Objective Collect(ItemData item, int count) => new() { Type = ObjectiveType.Collect, Item = item, Count = count };

        static void Item(string file, out ItemData item) => item = AssetDatabase.LoadAssetAtPath<ItemData>($"{Root}/Data/Items/{file}.asset");

        // ---------- Helpers ----------

        static void AddMissing(SerializedProperty list, Object[] values)
        {
            foreach (var value in values)
            {
                if (value == null) continue;
                bool present = false;
                for (int i = 0; i < list.arraySize && !present; i++)
                    present = list.GetArrayElementAtIndex(i).objectReferenceValue == value;
                if (present) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
            }
        }

        static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
