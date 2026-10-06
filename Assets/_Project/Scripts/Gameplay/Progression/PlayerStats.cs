using System;
using System.Collections.Generic;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Combines class base stats with every IStatSource on the player (Discipline levels, equipment,
    /// passive skills, buffs) and pushes the results into Combatant and Stamina.
    /// Sources call MarkDirty(); the recalculation happens once, at most once per frame.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(PlayerCombat))]
    public sealed class PlayerStats : MonoBehaviour
    {
        static readonly int StatCount = Enum.GetValues(typeof(StatType)).Length;

        readonly float[] values = new float[StatCount];
        readonly float[] flat = new float[StatCount];
        readonly float[] percent = new float[StatCount];
        readonly List<StatModifier> buffer = new();

        IStatSource[] sources;
        PlayerCombat combat;
        Combatant combatant;
        Stamina stamina;
        bool dirty = true;

        /// <summary>Raised after stats are recalculated (UI refresh).</summary>
        public event Action Changed;

        public float Get(StatType stat) => values[(int)stat];

        public void MarkDirty() => dirty = true;

        void Awake()
        {
            sources = GetComponents<IStatSource>();
            combat = GetComponent<PlayerCombat>();
            combatant = GetComponent<Combatant>();
            stamina = GetComponent<Stamina>();
        }

        void Update()
        {
            if (dirty) Recalculate();
        }

        void Recalculate()
        {
            dirty = false;
            Array.Clear(flat, 0, StatCount);
            Array.Clear(percent, 0, StatCount);

            buffer.Clear();
            foreach (var source in sources) source.CollectModifiers(buffer);
            foreach (var modifier in buffer)
            {
                flat[(int)modifier.Stat] += modifier.Flat;
                percent[(int)modifier.Stat] += modifier.Percent;
            }

            for (int i = 0; i < StatCount; i++)
                values[i] = Mathf.Max(0f, (BaseValue((StatType)i) + flat[i]) * (1f + percent[i]));

            Apply();
            Changed?.Invoke();
        }

        float BaseValue(StatType stat)
        {
            var data = combat.Class;
            return stat switch
            {
                StatType.MaxHealth => data != null ? data.MaxHealth : 100f,
                StatType.MaxPoise => data != null ? data.MaxPoise : 40f,
                StatType.MaxStamina => data != null ? data.MaxStamina : 100f,
                StatType.StaminaRegen => data != null ? data.StaminaRegen : 30f,
                StatType.AttackPower => 100f,
                StatType.PoiseDamage => 100f,
                _ => 0f,
            };
        }

        void Apply()
        {
            combatant.SetMaxStats(Get(StatType.MaxHealth), Get(StatType.MaxPoise));
            combatant.DamageMultiplier = Get(StatType.AttackPower) / 100f;
            combatant.PoiseDamageMultiplier = Get(StatType.PoiseDamage) / 100f;
            combatant.DamageTakenMultiplier = 100f / (100f + Get(StatType.Defense));
            if (stamina != null) stamina.SetMax(Get(StatType.MaxStamina), Get(StatType.StaminaRegen));
        }
    }
}
