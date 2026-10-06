using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    public enum ItemCategory
    {
        Material,
        Consumable,
        Seed,
        Crop,
        Equipment,
        Quest,
    }

    /// <summary>Any item that can sit in an inventory. Saves reference items by Id.</summary>
    [CreateAssetMenu(menuName = "Beast/Items/Item", fileName = "Item")]
    public class ItemData : GameData
    {
        public string DisplayName = "New Item";
        [TextArea] public string Description;
        public ItemCategory Category;

        [Header("Visuals")]
        [Tooltip("Optional. Until icons exist, UI and pickups use the placeholder colour.")]
        public Sprite Icon;
        public Color PlaceholderColor = Color.white;

        [Header("Stacking & Value")]
        [Min(1)] public int MaxStack = 99;
        [Tooltip("Base trade price in gold. The economy will adjust it (reputation, supply).")]
        [Min(0)] public int BaseValue = 1;
    }
}
