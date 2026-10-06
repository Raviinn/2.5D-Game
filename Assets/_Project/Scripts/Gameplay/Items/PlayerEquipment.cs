using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Two weapon slots (quick swap) plus Head, Body, Legs and Accessory. Equipping moves items out of the bag;
    /// unequipping puts them back. Only the ACTIVE weapon's stats and moveset apply.
    /// </summary>
    [RequireComponent(typeof(Inventory))]
    public sealed class PlayerEquipment : MonoBehaviour, ISaveable, IStatSource
    {
        public const int WeaponSlots = 2;
        static readonly EquipSlot[] ArmorSlots = { EquipSlot.Head, EquipSlot.Body, EquipSlot.Legs, EquipSlot.Accessory };

        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "player.equipment";
        [SerializeField] WeaponData startingMainWeapon;
        [SerializeField] WeaponData startingSecondWeapon;
        [SerializeField] EquipmentData[] startingArmor;

        readonly WeaponData[] weapons = new WeaponData[WeaponSlots];
        readonly Dictionary<EquipSlot, EquipmentData> armor = new();
        int activeWeapon;
        Inventory inventory;
        PlayerStats stats;

        [Serializable]
        sealed class State
        {
            public string[] weapons;
            public int activeWeapon;
            public string[] armorSlots;
            public string[] armorItems;
        }

        public string SaveId => saveId;
        public int ActiveWeaponIndex => activeWeapon;
        public WeaponData ActiveWeapon => weapons[activeWeapon];
        public WeaponData WeaponAt(int index) => weapons[index];
        public EquipmentData ArmorAt(EquipSlot slot) => armor.TryGetValue(slot, out var item) ? item : null;
        public static IReadOnlyList<EquipSlot> ArmorSlotOrder => ArmorSlots;

        /// <summary>Raised when anything is equipped, unequipped or swapped.</summary>
        public event Action Changed;

        void Awake()
        {
            inventory = GetComponent<Inventory>();
            stats = GetComponent<PlayerStats>();
            weapons[0] = startingMainWeapon;
            weapons[1] = startingSecondWeapon;
            if (startingArmor != null)
                foreach (var item in startingArmor)
                    if (item != null && item.Slot != EquipSlot.Weapon) armor[item.Slot] = item;
        }

        void OnEnable()
        {
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        /// <summary>Equips an item from the bag. The replaced item goes back into the bag.</summary>
        public bool Equip(EquipmentData item)
        {
            if (item == null || inventory.CountOf(item) <= 0) return false;

            if (item is WeaponData weapon)
            {
                int slot = weapons[0] == null ? 0 : weapons[1] == null ? 1 : activeWeapon;
                inventory.Remove(weapon, 1);
                if (weapons[slot] != null) inventory.Add(weapons[slot], 1);
                weapons[slot] = weapon;
            }
            else
            {
                if (item.Slot == EquipSlot.Weapon) return false; // misconfigured: weapons must be WeaponData
                inventory.Remove(item, 1);
                if (armor.TryGetValue(item.Slot, out var previous) && previous != null) inventory.Add(previous, 1);
                armor[item.Slot] = item;
            }
            NotifyChanged();
            return true;
        }

        public bool UnequipWeapon(int index)
        {
            var weapon = weapons[index];
            if (weapon == null || inventory.SpaceFor(weapon) <= 0) return false;
            inventory.Add(weapon, 1);
            weapons[index] = null;
            if (weapons[activeWeapon] == null && weapons[1 - activeWeapon] != null) activeWeapon = 1 - activeWeapon;
            NotifyChanged();
            return true;
        }

        public bool UnequipArmor(EquipSlot slot)
        {
            var item = ArmorAt(slot);
            if (item == null || inventory.SpaceFor(item) <= 0) return false;
            inventory.Add(item, 1);
            armor.Remove(slot);
            NotifyChanged();
            return true;
        }

        /// <summary>Switches to the other weapon (and its combat style). False if there's only one.</summary>
        public bool SwapWeapons()
        {
            int other = 1 - activeWeapon;
            if (weapons[other] == null) return false;
            activeWeapon = other;
            NotifyChanged();
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"Weapon: {weapons[other].DisplayName}"));
            return true;
        }

        public void CollectModifiers(List<StatModifier> into)
        {
            AddModifiers(ActiveWeapon, into);
            foreach (var item in armor.Values) AddModifiers(item, into);
        }

        static void AddModifiers(EquipmentData item, List<StatModifier> into)
        {
            if (item != null && item.Modifiers != null) into.AddRange(item.Modifiers);
        }

        void NotifyChanged()
        {
            if (stats != null) stats.MarkDirty();
            Changed?.Invoke();
        }

        // ---------- Save ----------

        public string CaptureState()
        {
            var state = new State
            {
                weapons = new string[WeaponSlots],
                activeWeapon = activeWeapon,
                armorSlots = new string[armor.Count],
                armorItems = new string[armor.Count],
            };
            for (int i = 0; i < WeaponSlots; i++) state.weapons[i] = weapons[i] != null ? weapons[i].Id : string.Empty;
            int n = 0;
            foreach (var pair in armor)
            {
                state.armorSlots[n] = pair.Key.ToString();
                state.armorItems[n] = pair.Value.Id;
                n++;
            }
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            if (!Services.TryGet(out GameDatabase database)) return;

            for (int i = 0; i < WeaponSlots; i++)
            {
                string id = state.weapons != null && i < state.weapons.Length ? state.weapons[i] : string.Empty;
                weapons[i] = !string.IsNullOrEmpty(id) && database.TryGet(id, out WeaponData weapon) ? weapon : null;
            }
            activeWeapon = Mathf.Clamp(state.activeWeapon, 0, WeaponSlots - 1);

            armor.Clear();
            if (state.armorSlots != null)
                for (int i = 0; i < state.armorSlots.Length; i++)
                    if (Enum.TryParse(state.armorSlots[i], out EquipSlot slot) && database.TryGet(state.armorItems[i], out EquipmentData item))
                        armor[slot] = item;

            NotifyChanged();
        }
    }
}
