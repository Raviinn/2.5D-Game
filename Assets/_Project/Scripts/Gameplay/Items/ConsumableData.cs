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

        void Reset() => Category = ItemCategory.Consumable;
    }
}
