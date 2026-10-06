using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum QuestType
    {
        Story,
        Side,
        /// <summary>Repeatable job from a contracts board (once per day).</summary>
        Contract,
    }

    public enum ObjectiveType
    {
        /// <summary>Defeat enemies (a specific EnemyData, or any enemy).</summary>
        Kill,
        /// <summary>Have items in the bag (counted live; optionally taken on turn-in).</summary>
        Collect,
        /// <summary>Harvest crops (a specific CropData, or any crop).</summary>
        Harvest,
        /// <summary>Completed from dialogue: complete_objective(quest, index).</summary>
        Custom,
    }

    /// <summary>A quest: objectives, rewards, and how it's turned in. Started and turned in from Ink or a contracts board.</summary>
    [CreateAssetMenu(menuName = "Beast/Quests/Quest", fileName = "Quest")]
    public sealed class QuestData : GameData
    {
        [Serializable]
        public sealed class Objective
        {
            public ObjectiveType Type;
            [Tooltip("Optional. Generated from the target when empty (required for Custom).")]
            public string Description;
            [Tooltip("Kill: which enemy (empty = any).")] public EnemyData Enemy;
            [Tooltip("Collect: which item.")] public ItemData Item;
            [Tooltip("Harvest: which crop (empty = any).")] public CropData Crop;
            [Min(1)] public int Count = 1;

            /// <summary>Objective text without the count (UIs append "(2/5)" or "×5").</summary>
            public string Text => !string.IsNullOrEmpty(Description) ? Description : Type switch
            {
                ObjectiveType.Kill => $"Defeat {(Enemy != null ? Enemy.DisplayName : "enemies")}",
                ObjectiveType.Collect => $"Bring {(Item != null ? Item.DisplayName : "items")}",
                ObjectiveType.Harvest => $"Harvest {(Crop != null ? Crop.DisplayName : "crops")}",
                _ => "Complete the task",
            };
        }

        [Serializable]
        public sealed class ItemReward
        {
            public ItemData Item;
            [Min(1)] public int Count = 1;
        }

        public string Title = "New Quest";
        [TextArea] public string Summary;
        public QuestType Type = QuestType.Side;
        [Tooltip("Who to return to (shown in the journal).")] public string TurnInAt;
        [Tooltip("Quests that must be completed before this one can start.")]
        public QuestData[] Prerequisites;
        [Min(0), Tooltip("Standing tier needed to start (index into the Reputation Config tiers: 0 = none, 1 Known, 2 Trusted, 3 Friend, 4 Hero).")]
        public int RequiredTier;

        [Header("Objectives")]
        public Objective[] Objectives;
        [Tooltip("Collect objectives remove the items from the bag on turn-in.")]
        public bool TakeItemsOnTurnIn = true;
        [Tooltip("Rewards are granted the moment all objectives are done (no need to return).")]
        public bool AutoComplete;

        [Header("Rewards")]
        [Min(0)] public int GoldReward;
        public ItemReward[] ItemRewards;
        [Min(0)] public int CombatXp;
        [Min(0)] public int FarmingXp;
        [Min(0), Tooltip("Standing with the Free Hollows gained on turn-in.")]
        public int StandingReward;

        public bool IsRepeatable => Type == QuestType.Contract;
    }
}
