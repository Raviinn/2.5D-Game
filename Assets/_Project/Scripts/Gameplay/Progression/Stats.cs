using System;
using System.Collections.Generic;

namespace Beast.Gameplay
{
    /// <summary>
    /// Every stat that equipment, Discipline levels, passive skills and buffs can change.
    /// Append new stats at the end (saved data and assets store them by index).
    /// </summary>
    public enum StatType
    {
        MaxHealth,
        MaxPoise,
        MaxStamina,
        StaminaRegen,
        /// <summary>100 = normal damage. Weapons add flat points; buffs add %.</summary>
        AttackPower,
        /// <summary>100 = normal poise damage.</summary>
        PoiseDamage,
        /// <summary>Damage taken × 100 / (100 + Defense). 100 defense = half damage.</summary>
        Defense,
        ParryStaminaRestore,
        /// <summary>Chance (0–1) of +1 produce per harvest.</summary>
        ExtraYieldChance,
        /// <summary>Extra dry days crops tolerate before wilting and dying.</summary>
        DroughtTolerance,
        /// <summary>Chance (0–1) that planting doesn't use up the seed.</summary>
        SeedSaveChance,
    }

    /// <summary>Final stat = (base + all Flat) × (1 + all Percent).</summary>
    [Serializable]
    public struct StatModifier
    {
        public StatType Stat;
        public float Flat;
        [UnityEngine.Tooltip("0.1 = +10%")] public float Percent;

        public StatModifier(StatType stat, float flat, float percent = 0f)
        {
            Stat = stat;
            Flat = flat;
            Percent = percent;
        }

        public StatModifier Scaled(float factor) => new(Stat, Flat * factor, Percent * factor);

        public override string ToString()
        {
            string flat = Flat != 0f ? $"{(Flat > 0f ? "+" : "")}{FormatFlat()}" : string.Empty;
            string percent = Percent != 0f ? $"{(Percent > 0f ? "+" : "")}{Percent * 100f:0}%" : string.Empty;
            return $"{flat}{(flat.Length > 0 && percent.Length > 0 ? " " : "")}{percent} {StatNames.Of(Stat)}";
        }

        string FormatFlat() => Stat is StatType.ExtraYieldChance or StatType.SeedSaveChance ? $"{Flat * 100f:0}%" : $"{Flat:0.#}";
    }

    /// <summary>Anything on the player that contributes stat modifiers (equipment, levels, skills).</summary>
    public interface IStatSource
    {
        void CollectModifiers(List<StatModifier> into);
    }

    public static class StatNames
    {
        public static string Of(StatType stat) => stat switch
        {
            StatType.MaxHealth => "Health",
            StatType.MaxPoise => "Poise",
            StatType.MaxStamina => "Stamina",
            StatType.StaminaRegen => "Stamina Regen",
            StatType.AttackPower => "Attack",
            StatType.PoiseDamage => "Poise Damage",
            StatType.Defense => "Defense",
            StatType.ParryStaminaRestore => "Stamina on Parry",
            StatType.ExtraYieldChance => "Extra Harvest Chance",
            StatType.DroughtTolerance => "Drought Tolerance (days)",
            StatType.SeedSaveChance => "Seed Saving Chance",
            _ => stat.ToString(),
        };
    }
}
