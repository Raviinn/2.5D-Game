using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Beast.Gameplay
{
    /// <summary>Each entry rolls independently. Gold is rolled separately.</summary>
    [CreateAssetMenu(menuName = "Beast/Items/Loot Table", fileName = "Loot")]
    public sealed class LootTable : GameData
    {
        [Serializable]
        public sealed class Entry
        {
            public ItemData Item;
            [Range(0f, 1f)] public float Chance = 1f;
            [Min(1)] public int Min = 1;
            [Min(1)] public int Max = 1;
        }

        public Entry[] Entries;
        [Min(0)] public int MinGold;
        [Min(0)] public int MaxGold;

        /// <summary>Appends dropped stacks to results (not cleared first).</summary>
        public void Roll(List<ItemStack> results, out int gold)
        {
            if (Entries != null)
            {
                foreach (var entry in Entries)
                {
                    if (entry.Item == null || Random.value > entry.Chance) continue;
                    results.Add(new ItemStack(entry.Item, Random.Range(entry.Min, Mathf.Max(entry.Min, entry.Max) + 1)));
                }
            }
            gold = MaxGold > 0 ? Random.Range(MinGold, Mathf.Max(MinGold, MaxGold) + 1) : 0;
        }
    }
}
