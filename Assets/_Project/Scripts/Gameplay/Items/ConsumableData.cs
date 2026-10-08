using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>Food and potions: used from the inventory or the quick-use button.</summary>
    [CreateAssetMenu(menuName = "Beast/Items/Consumable", fileName = "Consumable")]
    public sealed class ConsumableData : ItemData
    {
        [Header("Effects")]
        [Min(0f)] public float Heal;
        [Min(0f)] public float Stamina;

        [Header("Buff (optional)")]
        [Tooltip("A stat bonus that lasts a while after eating or drinking (e.g. Attack +10%). Using another of the same item restarts it.")]
        public StatModifier Buff;
        [Tooltip("Seconds the buff lasts. 0 = no buff.")]
        [Min(0f)] public float BuffSeconds;

        public bool HasBuff => BuffSeconds > 0f && (Buff.Flat != 0f || Buff.Percent != 0f);

        /// <summary>"Heals 45 · +30 stamina · +10% Attack for 3 min" — for tooltips and the crafting screen.</summary>
        public string EffectText()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (Heal > 0f) parts.Add($"Heals {Heal:0}");
            if (Stamina > 0f) parts.Add($"+{Stamina:0} stamina");
            if (HasBuff)
            {
                float minutes = BuffSeconds / 60f;
                string time = minutes >= 1f ? $"{minutes:0.#} min" : $"{BuffSeconds:0} s";
                parts.Add($"{Buff} for {time}");
            }
            return string.Join(" · ", parts);
        }

        void Reset() => Category = ItemCategory.Consumable;
    }
}
