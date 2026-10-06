using System;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Light reputation (v1): standing tiers with the Free Hollows, what each tier does to prices,
    /// and how fast standing is earned from kills and trade (daily caps stop grinding).
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Reputation/Reputation Config", fileName = "ReputationConfig")]
    public sealed class ReputationConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Tier
        {
            public string Name;
            [Min(0), Tooltip("Standing needed to reach this tier. The first tier must be 0; keep them in ascending order.")]
            public int MinStanding;
            [Range(0.5f, 1f), Tooltip("Shops multiply buy prices by this and divide sell prices by it (0.9 = 10% better both ways).")]
            public float PriceModifier = 1f;
            [Tooltip("Shown in the journal, e.g. \"10% better prices\".")]
            public string Perk;
        }

        public string FactionName = "Free Hollows";
        public Tier[] Tiers =
        {
            new() { Name = "Stranger", MinStanding = 0, PriceModifier = 1f, Perk = "Standard prices" },
            new() { Name = "Known", MinStanding = 50, PriceModifier = 0.95f, Perk = "5% better prices" },
            new() { Name = "Trusted", MinStanding = 150, PriceModifier = 0.9f, Perk = "10% better prices · Town Patrol contract" },
            new() { Name = "Friend", MinStanding = 300, PriceModifier = 0.85f, Perk = "15% better prices" },
            new() { Name = "Hero of the Hollows", MinStanding = 500, PriceModifier = 0.8f, Perk = "20% better prices" },
        };

        [Header("Earning")]
        [Min(0), Tooltip("Most standing that kills can earn per in-game day.")]
        public int DailyKillCap = 20;
        [Min(1), Tooltip("Gold traded (bought or sold) with town shops per point of standing.")]
        public int GoldPerTradePoint = 25;
        [Min(0), Tooltip("Most standing that trade can earn per in-game day.")]
        public int DailyTradeCap = 15;

        [Header("Losing")]
        [Min(0), Tooltip("Lost when abandoning a quest or contract that would have raised standing.")]
        public int AbandonPenalty = 5;

        public bool IsValid => Tiers != null && Tiers.Length > 0;

        /// <summary>Standing never goes above the last tier's threshold.</summary>
        public int MaxStanding => IsValid ? Tiers[Tiers.Length - 1].MinStanding : 0;

        public int TierAt(int standing)
        {
            int tier = 0;
            if (!IsValid) return tier;
            for (int i = 1; i < Tiers.Length; i++)
                if (standing >= Tiers[i].MinStanding) tier = i;
            return tier;
        }

        public string TierName(int index) => IsValid ? Tiers[Mathf.Clamp(index, 0, Tiers.Length - 1)].Name : "—";
    }
}
