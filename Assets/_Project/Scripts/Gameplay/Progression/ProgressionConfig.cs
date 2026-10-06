using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>A separately-levelled area of play. Append new ones at the end (saves store them by index).</summary>
    public enum Discipline
    {
        Combat,
        Farming,
    }

    /// <summary>XP curve and per-level stat growth for every Discipline.</summary>
    [CreateAssetMenu(menuName = "Beast/Progression/Progression Config", fileName = "ProgressionConfig")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        [Serializable]
        public sealed class DisciplineGrowth
        {
            public Discipline Discipline;
            [Tooltip("Added for every level above 1.")]
            public StatModifier[] PerLevel;
        }

        [Min(1)] public int LevelCap = 30;
        [Tooltip("XP needed to go from level L to L+1 = BaseXp × L^Exponent.")]
        public float BaseXp = 100f;
        public float Exponent = 1.5f;
        [Min(0)] public int PointsPerLevel = 1;
        public DisciplineGrowth[] Growth;

        public int XpToNext(int level) => Mathf.CeilToInt(BaseXp * Mathf.Pow(level, Exponent));

        public StatModifier[] GrowthFor(Discipline discipline)
        {
            if (Growth != null)
                foreach (var growth in Growth)
                    if (growth.Discipline == discipline) return growth.PerLevel;
            return Array.Empty<StatModifier>();
        }
    }

    public readonly struct XpGainedEvent : IEvent
    {
        public readonly Discipline Discipline;
        public readonly int Amount;
        public XpGainedEvent(Discipline discipline, int amount) { Discipline = discipline; Amount = amount; }
    }

    public readonly struct LevelUpEvent : IEvent
    {
        public readonly Discipline Discipline;
        public readonly int Level;
        public LevelUpEvent(Discipline discipline, int level) { Discipline = discipline; Level = level; }
    }
}
