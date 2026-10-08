using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum CraftKind
    {
        Alchemy,
        Cooking,
        Smithing,
    }

    /// <summary>
    /// One recipe: ingredients in, an item out. An ingredient that is a piece of gear may also be taken from what you
    /// have equipped; if the result is gear for the same slot it goes straight back on (upgrading in place).
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Crafting/Recipe", fileName = "Recipe")]
    public sealed class RecipeData : GameData
    {
        [Serializable]
        public struct Ingredient
        {
            public ItemData Item;
            [Min(1)] public int Count;
        }

        public CraftKind Kind;
        public Ingredient[] Inputs;
        public ItemData Output;
        [Min(1)] public int OutputCount = 1;

        public string DisplayName => Output != null ? (OutputCount > 1 ? $"{Output.DisplayName} ×{OutputCount}" : Output.DisplayName) : name;
    }
}
