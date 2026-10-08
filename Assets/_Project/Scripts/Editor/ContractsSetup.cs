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
    /// Milestone 48 setup (rotating contracts): eight new contracts (wolves, pelts, eggs, fish, mushrooms, pumpkins,
    /// and two gated ones: the shieldbearer for Trusted, the Blighted Brute for Friends of the Hollows) join the
    /// board's pool; the board posts four a day from it. Needs Milestones 40, 45, 46 and 47 for the targets.
    /// Safe to re-run: existing contracts keep your tuning; missing ones are added to the pool.
    /// </summary>
    public static class ContractsSetup
    {
        const string Root = "Assets/_Project";
        const string TestWorldScenePath = Root + "/Scenes/World_Test.unity";
        const string QuestsFolder = Root + "/Data/Quests";

        [MenuItem("Beast/Setup/Run Milestone 48 Setup (Rotating Contracts)", priority = 40)]
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
            var pelt = Load<ItemData>("Items/Item_WolfPelt");
            var egg = Load<ItemData>("Items/Item_Egg");
            var perch = Load<ItemData>("Items/Item_RiverPerch");
            var mushrooms = Load<ItemData>("Items/Item_ForestMushrooms");
            var pumpkin = Load<ItemData>("Items/Item_Pumpkin");
            if (wolf == null || shieldbearer == null || brute == null || pelt == null || egg == null || perch == null || mushrooms == null || pumpkin == null)
            {
                Debug.LogError("[Setup] Missing enemies or items. Run the Milestone 40, 45, 46 and 47 setups first.");
                return;
            }

            var created = new List<QuestData>
            {
                Contract("Contract_CullThePack", "Cull the Pack", "Wolves from the north-west woods are taking livestock. Two fewer would help.", 0,
                    Kill(wolf, 2), gold: 40, combat: 35, standing: 6),
                Contract("Contract_PeltsForWinter", "Pelts for Winter", "The refugee camp needs warm bedding before the cold. Bring wolf pelts.", 0,
                    Collect(pelt, 3), gold: 45, combat: 10, standing: 6),
                Contract("Contract_EggsForTheCamp", "Eggs for the Camp", "Fresh eggs for the children at the camp.", 0,
                    Collect(egg, 4), gold: 30, farming: 15, standing: 5),
                Contract("Contract_FreshFish", "Fresh Fish", "The inn wants fish on the table tonight. Perch will do.", 0,
                    Collect(perch, 2), gold: 35, farming: 15, standing: 5),
                Contract("Contract_MushroomBasket", "A Basket of Mushrooms", "Maren dries forest mushrooms for winter remedies.", 0,
                    Collect(mushrooms, 4), gold: 30, farming: 15, standing: 5),
                Contract("Contract_PumpkinOrder", "Pumpkin Order", "Oswin's buyer from the river towns wants pumpkins. Autumn only, obviously.", 0,
                    Collect(pumpkin, 3), gold: 90, farming: 25, standing: 8),
                Contract("Contract_ShieldBreaker", "Shield-Breaker", "The shieldbearer is back at the camp. The watch pays to have him put down again.", 2,
                    Kill(shieldbearer, 1), gold: 80, combat: 60, standing: 12),
                Contract("Contract_BruteHunt", "Brute Hunt", "The thing in the dead wood walks again at night. The Hollows pay well to see it fall.", 3,
                    Kill(brute, 1), gold: 180, combat: 120, standing: 20),
            };
            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(Root + "/Data/GameDatabase.asset");
            if (database != null) GameDatabaseBuilder.Rebuild(database);
            AssetDatabase.SaveAssets();
            var paths = created.ConvertAll(AssetDatabase.GetAssetPath);

            var scene = EditorSceneManager.OpenScene(TestWorldScenePath);
            var board = Object.FindFirstObjectByType<ContractBoard>();
            if (board == null)
            {
                Debug.LogError("[Setup] No contracts board in World_Test. Run the Milestone 8 setup first.");
                return;
            }
            var so = new SerializedObject(board);
            var pool = so.FindProperty("contracts");
            int added = 0;
            foreach (var path in paths)
            {
                var contract = AssetDatabase.LoadAssetAtPath<QuestData>(path);
                bool present = false;
                for (int i = 0; i < pool.arraySize && !present; i++) present = pool.GetArrayElementAtIndex(i).objectReferenceValue == contract;
                if (present) continue;
                pool.arraySize++;
                pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = contract;
                added++;
            }
            so.FindProperty("postedPerDay").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] Milestone 48 rotating contracts setup complete: {added} contract(s) added to the board's pool ({pool.arraySize} in all). " +
                      "The board now posts four a day; taken contracts stay up until handed in.");
        }

        static QuestData.Objective Kill(EnemyData enemy, int count) => new() { Type = ObjectiveType.Kill, Enemy = enemy, Count = count };
        static QuestData.Objective Collect(ItemData item, int count) => new() { Type = ObjectiveType.Collect, Item = item, Count = count };

        static QuestData Contract(string file, string title, string summary, int tier, QuestData.Objective objective,
            int gold = 0, int combat = 0, int farming = 0, int standing = 0)
        {
            string path = $"{QuestsFolder}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<QuestData>(path);
            if (existing != null) return existing;
            var q = ScriptableObject.CreateInstance<QuestData>();
            q.Title = title;
            q.Summary = summary;
            q.Type = QuestType.Contract;
            q.TurnInAt = "the Contracts Board";
            q.RequiredTier = tier;
            q.Objectives = new[] { objective };
            q.TakeItemsOnTurnIn = true;
            q.GoldReward = gold;
            q.CombatXp = combat;
            q.FarmingXp = farming;
            q.StandingReward = standing;
            AssetDatabase.CreateAsset(q, path);
            return q;
        }
    }
}
