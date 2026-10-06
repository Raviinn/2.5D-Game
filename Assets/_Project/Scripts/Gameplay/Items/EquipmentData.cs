using UnityEngine;

namespace Beast.Gameplay
{
    public enum EquipSlot
    {
        Weapon,
        Head,
        Body,
        Legs,
        Accessory,
    }

    /// <summary>Wearable item. Its modifiers apply while equipped (weapons: only while they're the active weapon).</summary>
    [CreateAssetMenu(menuName = "Beast/Items/Equipment", fileName = "Equipment")]
    public class EquipmentData : ItemData
    {
        [Header("Equipment")]
        public EquipSlot Slot = EquipSlot.Body;
        public StatModifier[] Modifiers;

        protected virtual void Reset()
        {
            Category = ItemCategory.Equipment;
            MaxStack = 1;
        }
    }
}
