using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>The recipes a workbench offers, in display order.</summary>
    [CreateAssetMenu(menuName = "Beast/Crafting/Recipe Book", fileName = "RecipeBook")]
    public sealed class RecipeBook : ScriptableObject
    {
        public RecipeData[] Recipes;
    }
}
