using System.IO;
using Beast.Core;
using Beast.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Beast.EditorTools
{
    /// <summary>
    /// Milestone 10 setup (light reputation): creates the Reputation Config, gives quests and bandits their
    /// standing rewards, adds the Town Patrol contract (needs Trusted standing), recompiles the Ink story,
    /// and adds the Reputation component to the player in World_Test.
    /// Safe to re-run; existing assets keep your tuning (only standing rewards that are still 0 are filled in).
    /// </summary>
    public static class ReputationSetup
    {
        const string Root = "Assets/_Project";
        const string ConfigFolder = Root + "/Data/Reputation";
        const string ConfigPath = ConfigFolder + "/ReputationConfig.asset";
        const string QuestsFolder = Root + "/Data/Quests";
        const string BanditPath = Root + "/Data/Combat/Enemies/Enemy_Bandit.asset";
        const string PatrolPath = QuestsFolder + "/Contract_TownPatrol.asset";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const int TrustedTier = 2;

        [MenuItem("Beast/Setup/Run Milestone 10 Setup (Reputation)", priority = 9)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var bandit = AssetDatabase.LoadAssetAtPath<EnemyData>(BanditPath);
            if (bandit == null || !File.Exists(TestWorldScenePath) ||
                AssetDatabase.LoadAssetAtPath<QuestData>(QuestsFolder + "/Quest_BanditTrouble.asset") == null)
            {
                Debug.LogError("[Setup] Run the Milestone 1–8 setups first (enemies, quests and World_Test are required).");
                return;
            }

            if (!DialogueQuestSetup.CompileInk()) return; // the new standing dialogue must compile

            EnsureFolder(ConfigFolder);
            if (AssetDatabase.LoadAssetAtPath<ReputationConfig>(ConfigPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ReputationConfig>(), ConfigPath);

            // Standing rewards: story quests matter most, contracts a little every day.
            SetQuestStanding("Quest_BanditTrouble", 40);
            SetQuestStanding("Quest_RootsOfTheBlight", 40);
            SetQuestStanding("Quest_TasteOfHome", 25);
            SetQuestStanding("Quest_ScrapRun", 20);
            SetQuestStanding("Contract_BanditBounty", 10);
            SetQuestStanding("Contract_SupplyRun", 10);
            SetQuestStanding("Contract_Bandages", 8);
            if (bandit.StandingReward == 0)
            {
                bandit.StandingReward = 2;
                EditorUtility.SetDirty(bandit);
            }

            if (AssetDatabase.LoadAssetAtPath<QuestData>(PatrolPath) == null)
            {
                var patrol = ScriptableObject.CreateInstance<QuestData>();
                patrol.Title = "Town Patrol";
                patrol.Summary = "The guards trust you with the long road now. Clear five deserters from it.";
                patrol.Type = QuestType.Contract;
                patrol.TurnInAt = "the Contracts Board";
                patrol.RequiredTier = TrustedTier;
                patrol.Objectives = new[] { new QuestData.Objective { Type = ObjectiveType.Kill, Enemy = bandit, Count = 5 } };
                patrol.GoldReward = 90;
                patrol.CombatXp = 70;
                patrol.StandingReward = 20;
                AssetDatabase.CreateAsset(patrol, PatrolPath);
            }

            AssetDatabase.SaveAssets();
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            // Opening a scene unloads assets; reload by path so the scene stores valid references.
            var config = AssetDatabase.LoadAssetAtPath<ReputationConfig>(ConfigPath);
            var patrolContract = AssetDatabase.LoadAssetAtPath<QuestData>(PatrolPath);

            var player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("[Setup] No object tagged Player in World_Test.");
                return;
            }
            if (!player.TryGetComponent(out Reputation reputation)) reputation = player.AddComponent<Reputation>();
            var so = new SerializedObject(reputation);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            var board = Object.FindFirstObjectByType<ContractBoard>();
            if (board != null)
            {
                var boardSo = new SerializedObject(board);
                var contracts = boardSo.FindProperty("contracts");
                bool present = false;
                for (int i = 0; i < contracts.arraySize && !present; i++)
                    present = contracts.GetArrayElementAtIndex(i).objectReferenceValue == patrolContract;
                if (!present)
                {
                    contracts.arraySize++;
                    contracts.GetArrayElementAtIndex(contracts.arraySize - 1).objectReferenceValue = patrolContract;
                    boardSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            else Debug.LogWarning("[Setup] No contracts board in World_Test; the Town Patrol contract wasn't posted.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Setup] Milestone 10 reputation setup complete. Your standing shows in the journal (J); F8 grants 50 for testing.");
        }

        static void SetQuestStanding(string file, int standing)
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestData>($"{QuestsFolder}/{file}.asset");
            if (quest == null || quest.StandingReward != 0) return;
            quest.StandingReward = standing;
            EditorUtility.SetDirty(quest);
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
