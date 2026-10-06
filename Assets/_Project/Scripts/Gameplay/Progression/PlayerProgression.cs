using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Discipline levels: each one levels from its own activity (Combat from fights, Farming from harvests),
    /// grows its own stats and earns points for its own skill tree. Renown = sum of all levels (display only).
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public sealed class PlayerProgression : MonoBehaviour, ISaveable, IStatSource, ISaveSummarySource
    {
        static readonly int DisciplineCount = Enum.GetValues(typeof(Discipline)).Length;

        [SerializeField] ProgressionConfig config;
        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "player.progression";
        [SerializeField, Tooltip("Combat XP for a successful parry.")] int parryXp = 3;

        readonly int[] levels = new int[DisciplineCount];
        readonly int[] xp = new int[DisciplineCount];
        readonly int[] points = new int[DisciplineCount];

        Combatant self;
        PlayerStats stats;

        [Serializable]
        sealed class State
        {
            public int[] levels;
            public int[] xp;
            public int[] points;
        }

        public string SaveId => saveId;
        public ProgressionConfig Config => config;
        public int Renown { get { int sum = 0; foreach (int level in levels) sum += level; return sum; } }

        public int Level(Discipline d) => levels[(int)d];
        public int Xp(Discipline d) => xp[(int)d];
        public int Points(Discipline d) => points[(int)d];
        public bool IsMaxLevel(Discipline d) => config != null && levels[(int)d] >= config.LevelCap;
        public int XpToNext(Discipline d) => config != null ? config.XpToNext(levels[(int)d]) : 0;

        void Awake()
        {
            self = GetComponent<Combatant>();
            stats = GetComponent<PlayerStats>();
            for (int i = 0; i < DisciplineCount; i++) levels[i] = 1;
            if (config == null) Debug.LogError("[Progression] No ProgressionConfig assigned.", this);
        }

        void OnEnable()
        {
            EventBus<DamageDealtEvent>.Subscribe(OnDamageDealt);
            EventBus<CropHarvestedEvent>.Subscribe(OnCropHarvested);
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            EventBus<DamageDealtEvent>.Unsubscribe(OnDamageDealt);
            EventBus<CropHarvestedEvent>.Unsubscribe(OnCropHarvested);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        // ---------- XP sources ----------

        void OnDamageDealt(DamageDealtEvent evt)
        {
            if (evt.Result == HitResult.Killed && evt.Attacker == self &&
                evt.Target.TryGetComponent(out EnemyController enemy) && enemy.Data != null)
                AddXp(Discipline.Combat, enemy.Data.XpReward);
            else if (evt.Result == HitResult.Parried && evt.Target == self)
                AddXp(Discipline.Combat, parryXp);
        }

        void OnCropHarvested(CropHarvestedEvent evt) => AddXp(Discipline.Farming, evt.Crop.HarvestXp * evt.Count);

        // ---------- Levels ----------

        public void AddXp(Discipline discipline, int amount)
        {
            if (config == null || amount <= 0 || IsMaxLevel(discipline)) return;

            int d = (int)discipline;
            xp[d] += amount;
            EventBus<XpGainedEvent>.Raise(new XpGainedEvent(discipline, amount));

            while (!IsMaxLevel(discipline) && xp[d] >= config.XpToNext(levels[d]))
            {
                xp[d] -= config.XpToNext(levels[d]);
                levels[d]++;
                points[d] += config.PointsPerLevel;
                EventBus<LevelUpEvent>.Raise(new LevelUpEvent(discipline, levels[d]));
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"{discipline} level {levels[d]}!  (+{config.PointsPerLevel} skill point)"));
            }
            if (IsMaxLevel(discipline)) xp[d] = 0;
            if (stats != null) stats.MarkDirty();
        }

        public bool SpendPoints(Discipline discipline, int amount)
        {
            if (amount < 0 || points[(int)discipline] < amount) return false;
            points[(int)discipline] -= amount;
            return true;
        }

        public void CollectModifiers(List<StatModifier> into)
        {
            if (config == null) return;
            for (int d = 0; d < DisciplineCount; d++)
            {
                int bonusLevels = levels[d] - 1;
                if (bonusLevels <= 0) continue;
                foreach (var modifier in config.GrowthFor((Discipline)d)) into.Add(modifier.Scaled(bonusLevels));
            }
        }

        // ---------- Save ----------

        public void Describe(SaveSummary summary) =>
            summary.details.Add($"Combat {Level(Discipline.Combat)}, Farming {Level(Discipline.Farming)}");

        public string CaptureState() => JsonUtility.ToJson(new State
        {
            levels = (int[])levels.Clone(),
            xp = (int[])xp.Clone(),
            points = (int[])points.Clone(),
        });

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            for (int i = 0; i < DisciplineCount; i++)
            {
                levels[i] = state.levels != null && i < state.levels.Length ? Mathf.Max(1, state.levels[i]) : 1;
                xp[i] = state.xp != null && i < state.xp.Length ? state.xp[i] : 0;
                points[i] = state.points != null && i < state.points.Length ? state.points[i] : 0;
            }
            if (stats != null) stats.MarkDirty();
        }
    }
}
