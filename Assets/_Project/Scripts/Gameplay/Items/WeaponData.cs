using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>A weapon: its moveset IS the combat style (Sword &amp; Shield, Greatsword, later Bow, Staff...).</summary>
    [CreateAssetMenu(menuName = "Beast/Items/Weapon", fileName = "Weapon")]
    public sealed class WeaponData : EquipmentData
    {
        [Tooltip("The combat style this weapon uses.")]
        public MovesetData Moveset;

        protected override void Reset()
        {
            base.Reset();
            Slot = EquipSlot.Weapon;
        }
    }
}
