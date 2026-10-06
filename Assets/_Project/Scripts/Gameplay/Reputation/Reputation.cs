using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum StandingSource
    {
        Quest,
        Kill,
        Trade,
        Abandon,
        Dialogue,
        Debug,
    }

    /// <summary>Standing changed. Tier is an index into ReputationConfig.Tiers.</summary>
    public readonly struct ReputationChangedEvent : IEvent
    {
        public readonly int Standing;
        public readonly int Delta;
        public readonly int PreviousTier;
        public readonly int Tier;
        public readonly StandingSource Source;

        public ReputationChangedEvent(int standing, int delta, int previousTier, int tier, StandingSource source)
        {
            Standing = standing; Delta = delta; PreviousTier = previousTier; Tier = tier; Source = source;
        }

        public bool TierChanged => Tier != PreviousTier;
    }

    /// <summary>
    /// The player's standing with the Free Hollows (v1: one town, one value, never below 0).
    /// Raised by finishing quests, defeating enemies that threaten the region and trading with town shops;
    /// lowered by abandoning town work. Shops scale their prices by the current tier and quests can require one.
    /// Registered as a service so shops can find it.
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public sealed class Reputation : MonoBehaviour, ISaveable, ISaveSummarySource
    {
        [SerializeField] ReputationConfig config;
        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "player.reputation";

        int standing;
        // Daily caps: reset when the in-game day changes.
        int day = -1;
        int killStandingToday;
        int tradeStandingToday;
        int tradeGoldBank; // gold traded that hasn't made a whole point yet

        Combatant self;

        [Serializable]
        sealed class State
        {
            public int standing;
            public int day;
            public int killStandingToday;
            public int tradeStandingToday;
            public int tradeGoldBank;
        }

        public string SaveId => saveId;
        public ReputationConfig Config => config;
        public bool IsConfigured => config != null && config.IsValid;
        public string FactionName => IsConfigured ? config.FactionName : "the town";
        public int Standing => standing;
        public int TierIndex => IsConfigured ? config.TierAt(standing) : 0;
        public ReputationConfig.Tier Tier => IsConfigured ? config.Tiers[TierIndex] : null;
        public string TierName => IsConfigured ? config.TierName(TierIndex) : "—";
        public bool IsMaxTier => !IsConfigured || TierIndex >= config.Tiers.Length - 1;
        public ReputationConfig.Tier NextTier => IsMaxTier ? null : config.Tiers[TierIndex + 1];

        /// <summary>Shops multiply buy prices by this and divide sell prices by it. 1 = neutral.</summary>
        public float PriceModifier => Tier != null ? Tier.PriceModifier : 1f;

        /// <summary>"10% better prices" style percentage, 0 at neutral.</summary>
        public int PriceBonusPercent => Mathf.RoundToInt((1f - PriceModifier) * 100f);

        public bool MeetsTier(int tier) => tier <= 0 || TierIndex >= tier;
        public string NameOfTier(int tier) => IsConfigured ? config.TierName(tier) : "—";

        /// <summary>What abandoning this quest would cost (only town work that would have raised standing).</summary>
        public int AbandonPenaltyFor(QuestData quest) =>
            IsConfigured && quest != null && quest.StandingReward > 0 ? Mathf.Min(config.AbandonPenalty, standing) : 0;

        void Awake()
        {
            self = GetComponent<Combatant>();
            if (!IsConfigured) Debug.LogError("[Reputation] No ReputationConfig (with tiers) assigned. Run Beast > Setup > Run Milestone 10 Setup (Reputation).", this);
            Services.Register(this);
        }

        void OnDestroy() => Services.Unregister(this);

        void OnEnable()
        {
            EventBus<QuestCompletedEvent>.Subscribe(OnQuestCompleted);
            EventBus<QuestAbandonedEvent>.Subscribe(OnQuestAbandoned);
            EventBus<DamageDealtEvent>.Subscribe(OnDamageDealt);
            EventBus<ItemTradedEvent>.Subscribe(OnItemTraded);
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            EventBus<QuestCompletedEvent>.Unsubscribe(OnQuestCompleted);
            EventBus<QuestAbandonedEvent>.Unsubscribe(OnQuestAbandoned);
            EventBus<DamageDealtEvent>.Unsubscribe(OnDamageDealt);
            EventBus<ItemTradedEvent>.Unsubscribe(OnItemTraded);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        // ---------- Sources ----------

        void OnQuestCompleted(QuestCompletedEvent evt) => Change(evt.Quest.StandingReward, StandingSource.Quest);

        void OnQuestAbandoned(QuestAbandonedEvent evt) => Change(-AbandonPenaltyFor(evt.Quest), StandingSource.Abandon);

        void OnDamageDealt(DamageDealtEvent evt)
        {
            if (evt.Result != HitResult.Killed || evt.Attacker != self) return;
            if (evt.Target.TryGetComponent(out EnemyController enemy) && enemy.Data != null)
                Change(enemy.Data.StandingReward, StandingSource.Kill);
        }

        void OnItemTraded(ItemTradedEvent evt)
        {
            if (!IsConfigured || evt.Gold <= 0) return;
            tradeGoldBank += evt.Gold;
            int points = tradeGoldBank / config.GoldPerTradePoint;
            tradeGoldBank %= config.GoldPerTradePoint;
            Change(points, StandingSource.Trade);
        }

        // ---------- Changing standing ----------

        /// <summary>
        /// Adds (or removes) standing. Kill and trade gains respect the daily caps.
        /// Returns the change actually applied (after caps and the 0…max range).
        /// </summary>
        public int Change(int amount, StandingSource source)
        {
            if (!IsConfigured || amount == 0) return 0;
            RollDay();

            if (amount > 0 && source == StandingSource.Kill) amount = Capped(amount, ref killStandingToday, config.DailyKillCap);
            else if (amount > 0 && source == StandingSource.Trade) amount = Capped(amount, ref tradeStandingToday, config.DailyTradeCap);
            if (amount == 0) return 0;

            int previous = standing;
            int previousTier = TierIndex;
            standing = Mathf.Clamp(standing + amount, 0, config.MaxStanding);
            int delta = standing - previous;
            if (delta != 0)
                EventBus<ReputationChangedEvent>.Raise(new ReputationChangedEvent(standing, delta, previousTier, TierIndex, source));
            return delta;
        }

        static int Capped(int amount, ref int earnedToday, int cap)
        {
            int allowed = Mathf.Clamp(cap - earnedToday, 0, amount);
            earnedToday += allowed;
            return allowed;
        }

        void RollDay()
        {
            if (!Services.TryGet(out WorldClock clock) || clock.Day == day) return;
            day = clock.Day;
            killStandingToday = 0;
            tradeStandingToday = 0;
        }

        // ---------- Save ----------

        public void Describe(SaveSummary summary)
        {
            if (IsConfigured) summary.details.Add(TierName);
        }

        public string CaptureState() => JsonUtility.ToJson(new State
        {
            standing = standing,
            day = day,
            killStandingToday = killStandingToday,
            tradeStandingToday = tradeStandingToday,
            tradeGoldBank = tradeGoldBank,
        });

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            standing = IsConfigured ? Mathf.Clamp(state.standing, 0, config.MaxStanding) : Mathf.Max(0, state.standing);
            day = state.day;
            killStandingToday = state.killStandingToday;
            tradeStandingToday = state.tradeStandingToday;
            tradeGoldBank = state.tradeGoldBank;
        }
    }
}
