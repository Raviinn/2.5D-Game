using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum FatigueLevel
    {
        Rested,
        Tired,
        Exhausted,
    }

    public readonly struct FatigueChangedEvent : IEvent
    {
        public readonly FatigueLevel Previous;
        public readonly FatigueLevel Current;
        public FatigueChangedEvent(FatigueLevel previous, FatigueLevel current) { Previous = previous; Current = current; }
    }

    /// <summary>
    /// Fatigue: hours awake count up with the world clock; staying up too long makes you Tired, then Exhausted,
    /// which lowers stamina (and, exhausted, attack) until you sleep. Never kills you — it's pressure to go home.
    /// A stat source on the player, so the character screen shows the penalties.
    /// </summary>
    public sealed class PlayerFatigue : MonoBehaviour, IStatSource, ISaveable
    {
        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "player.fatigue";
        [SerializeField, Tooltip("Hours awake when a new game starts (it starts at 08:00, as if you woke at 06:00).")] float startHoursAwake = 2f;
        [SerializeField] float tiredAfterHours = 18f;
        [SerializeField] float exhaustedAfterHours = 22f;
        [SerializeField] StatModifier[] tiredPenalties =
        {
            new(StatType.MaxStamina, 0f, -0.15f),
            new(StatType.StaminaRegen, 0f, -0.2f),
        };
        [SerializeField] StatModifier[] exhaustedPenalties =
        {
            new(StatType.MaxStamina, 0f, -0.3f),
            new(StatType.StaminaRegen, 0f, -0.4f),
            new(StatType.AttackPower, 0f, -0.15f),
        };

        float hoursAwake;
        double lastMinutes = -1;
        FatigueLevel level;
        WorldClock clock;
        PlayerStats stats;

        [Serializable]
        sealed class State
        {
            public float hoursAwake;
        }

        public string SaveId => saveId;
        public float HoursAwake => hoursAwake;
        public FatigueLevel Level => level;
        public float TiredAfterHours => tiredAfterHours;
        public float ExhaustedAfterHours => exhaustedAfterHours;

        void Awake()
        {
            stats = GetComponent<PlayerStats>();
            hoursAwake = startHoursAwake;
        }

        void OnEnable()
        {
            EventBus<SleptEvent>.Subscribe(OnSlept);
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            EventBus<SleptEvent>.Unsubscribe(OnSlept);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void Update()
        {
            if (clock == null && !Services.TryGet(out clock)) return;
            double now = clock.TotalMinutes;
            if (lastMinutes >= 0 && now > lastMinutes) hoursAwake += (float)((now - lastMinutes) / 60.0);
            lastMinutes = now;
            Refresh(announce: true);
        }

        void OnSlept(SleptEvent evt)
        {
            hoursAwake = 0f;
            lastMinutes = clock != null ? clock.TotalMinutes : -1; // the night's skip isn't time awake
            Refresh(announce: false);
        }

        void Refresh(bool announce)
        {
            var next = hoursAwake >= exhaustedAfterHours ? FatigueLevel.Exhausted
                : hoursAwake >= tiredAfterHours ? FatigueLevel.Tired
                : FatigueLevel.Rested;
            if (next == level) return;

            var previous = level;
            level = next;
            if (stats != null) stats.MarkDirty();
            EventBus<FatigueChangedEvent>.Raise(new FatigueChangedEvent(previous, next));
            if (!announce || next < previous) return;
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent(next == FatigueLevel.Tired
                ? "You're getting tired: less stamina until you sleep."
                : "You're exhausted: stamina and attack suffer until you sleep."));
        }

        public void CollectModifiers(List<StatModifier> into)
        {
            var penalties = level switch
            {
                FatigueLevel.Tired => tiredPenalties,
                FatigueLevel.Exhausted => exhaustedPenalties,
                _ => null,
            };
            if (penalties != null) into.AddRange(penalties);
        }

        public string CaptureState() => JsonUtility.ToJson(new State { hoursAwake = hoursAwake });

        public void RestoreState(string json)
        {
            hoursAwake = Mathf.Max(0f, JsonUtility.FromJson<State>(json).hoursAwake);
            lastMinutes = -1; // the clock was restored too: re-baseline instead of counting the jump
            Refresh(announce: false);
        }
    }
}
