using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Milestone 44: gear wears out. The active weapon wears a little with every blow it lands, worn armour (head,
    /// body, legs) with every hit you take. Condition is kept per kind of item you own ("your Iron Helm"), equipped or
    /// not; once you own none of an item its wear is forgotten, so a new one is pristine.
    /// Worn below a quarter: a warning. Worn out: "broken", its stat bonuses stop until it's repaired, by Brenna for
    /// gold (repair_gear in Ink) or at the workbench with Iron Scrap.
    /// Items with Durability 0 (accessories, anything you set to 0) never wear.
    /// </summary>
    [RequireComponent(typeof(PlayerEquipment), typeof(Inventory))]
    public sealed class GearCondition : MonoBehaviour, ISaveable
    {
        public const float WornThreshold = 0.25f;
        static readonly EquipSlot[] WearingSlots = { EquipSlot.Head, EquipSlot.Body, EquipSlot.Legs };

        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "player.gear";
        [SerializeField, Tooltip("Gold to fully repair an item: its base value × this.")] float repairPriceShare = 0.6f;
        [SerializeField, Tooltip("Iron Scrap to fully repair an item at the workbench.")] int repairScrap = 4;

        readonly Dictionary<EquipmentData, float> wear = new();
        PlayerEquipment equipment;
        Inventory inventory;
        Combatant combatant;
        PlayerStats stats;
        bool forgetQueued;

        [Serializable]
        sealed class State
        {
            public string[] items;
            public float[] wear;
        }

        public string SaveId => saveId;
        public int ScrapForFullRepair => repairScrap;

        /// <summary>Raised whenever any item's condition changes (wear or repair).</summary>
        public event Action Changed;

        void Awake()
        {
            equipment = GetComponent<PlayerEquipment>();
            inventory = GetComponent<Inventory>();
            combatant = GetComponent<Combatant>();
            stats = GetComponent<PlayerStats>();
        }

        void OnEnable()
        {
            EventBus<DamageDealtEvent>.Subscribe(OnDamage);
            if (Services.TryGet(out SaveService save)) save.Register(this);
            inventory.Changed += QueueForget;
            equipment.Changed += QueueForget;
        }

        void OnDisable()
        {
            EventBus<DamageDealtEvent>.Unsubscribe(OnDamage);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
            inventory.Changed -= QueueForget;
            equipment.Changed -= QueueForget;
        }

        // Checked a frame later: while a save loads, the bag and the equipment are restored one after the other.
        void QueueForget() => forgetQueued = true;

        void LateUpdate()
        {
            if (!forgetQueued) return;
            forgetQueued = false;
            ForgetUnowned();
        }

        // ---------- Queries ----------

        public static bool Wears(EquipmentData item) => item != null && item.Durability > 0;

        /// <summary>1 = as new, 0 = broken. Always 1 for items that don't wear.</summary>
        public float ConditionOf(EquipmentData item) =>
            Wears(item) && wear.TryGetValue(item, out float worn) ? Mathf.Clamp01(1f - worn) : 1f;

        public bool IsBroken(EquipmentData item) => Wears(item) && ConditionOf(item) <= 0f;
        public bool IsWorn(EquipmentData item) => Wears(item) && ConditionOf(item) < 1f;

        /// <summary>Gold Brenna asks to make this item as good as new (0 when it isn't worn).</summary>
        public int RepairPrice(EquipmentData item) =>
            IsWorn(item) ? Mathf.Max(1, Mathf.CeilToInt((1f - ConditionOf(item)) * item.BaseValue * repairPriceShare)) : 0;

        /// <summary>Iron Scrap the workbench needs to make this item as good as new.</summary>
        public int RepairScrap(EquipmentData item) =>
            IsWorn(item) ? Mathf.Max(1, Mathf.CeilToInt((1f - ConditionOf(item)) * repairScrap)) : 0;

        /// <summary>Every worn item you own, most worn first.</summary>
        public List<EquipmentData> WornItems()
        {
            var list = new List<EquipmentData>();
            foreach (var pair in wear) if (pair.Value > 0f && pair.Key != null) list.Add(pair.Key);
            list.Sort((a, b) => ConditionOf(a).CompareTo(ConditionOf(b)));
            return list;
        }

        public int TotalRepairPrice()
        {
            int total = 0;
            foreach (var item in WornItems()) total += RepairPrice(item);
            return total;
        }

        /// <summary>"72%", in red when worn below a quarter or broken.</summary>
        public string ConditionText(EquipmentData item)
        {
            if (!Wears(item)) return string.Empty;
            float condition = ConditionOf(item);
            if (condition <= 0f) return $"<color={UITheme.BadHex}>Broken</color>";
            string text = $"Condition {condition * 100f:0}%";
            return condition < WornThreshold ? $"<color={UITheme.BadHex}>{text}</color>" : text;
        }

        // ---------- Wear ----------

        void OnDamage(DamageDealtEvent evt)
        {
            if (combatant == null) return;
            bool landed = evt.Result is HitResult.Hit or HitResult.Killed or HitResult.Blocked or HitResult.GuardBroken;
            if (!landed) return;
            if (evt.Attacker == combatant && evt.Target != combatant)
            {
                // Practising on the training dummy is free.
                if (evt.Target != null && evt.Target.TryGetComponent(out EnemyController enemy) && enemy.Data != null && enemy.Data.IsTrainingDummy) return;
                Wear(equipment.ActiveWeapon, 1f);
            }
            else if (evt.Target == combatant && evt.Attacker != null)
            {
                foreach (var slot in WearingSlots) Wear(equipment.ArmorAt(slot), 1f);
            }
        }

        /// <summary>Wears an item by this many uses (out of its Durability).</summary>
        public void Wear(EquipmentData item, float uses)
        {
            if (!Wears(item) || uses <= 0f) return;
            float before = ConditionOf(item);
            if (before <= 0f) return;
            wear.TryGetValue(item, out float worn);
            wear[item] = Mathf.Min(1f, worn + uses / item.Durability);
            float after = ConditionOf(item);
            if (after <= 0f)
            {
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"{item.DisplayName} broke! No bonuses until it's repaired."));
                if (stats != null) stats.MarkDirty();
            }
            else if (before >= WornThreshold && after < WornThreshold)
            {
                EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"{item.DisplayName} is badly worn. Repair it soon."));
            }
            Changed?.Invoke();
        }

        // ---------- Repair ----------

        public void Repair(EquipmentData item)
        {
            if (item == null || !wear.Remove(item)) return;
            if (stats != null) stats.MarkDirty();
            Changed?.Invoke();
        }

        /// <summary>Brenna: pays and repairs everything. False if there's nothing to repair or not enough gold.</summary>
        public bool RepairAllForGold()
        {
            int price = TotalRepairPrice();
            if (price <= 0 || !inventory.TrySpendGold(price)) return false;
            foreach (var item in WornItems()) wear.Remove(item);
            if (stats != null) stats.MarkDirty();
            Changed?.Invoke();
            EventBus<HudMessageEvent>.Raise(new HudMessageEvent($"Gear repaired for {price} gold"));
            return true;
        }

        /// <summary>Workbench: uses Iron Scrap (from the bag, then the storage chest) to repair one item.</summary>
        public bool RepairWithScrap(EquipmentData item, ItemData scrap, Inventory storage)
        {
            int needed = RepairScrap(item);
            if (needed <= 0 || scrap == null) return false;
            int inBag = inventory.CountOf(scrap);
            int inStorage = storage != null ? storage.CountOf(scrap) : 0;
            if (inBag + inStorage < needed) return false;
            int fromBag = Mathf.Min(inBag, needed);
            if (fromBag > 0) inventory.Remove(scrap, fromBag);
            if (needed - fromBag > 0) storage.Remove(scrap, needed - fromBag);
            Repair(item);
            return true;
        }

        /// <summary>Forgets wear on items you no longer own anywhere, so the next one you get is new.</summary>
        void ForgetUnowned()
        {
            if (wear.Count == 0) return;
            List<EquipmentData> gone = null;
            foreach (var item in wear.Keys)
            {
                if (inventory.CountOf(item) > 0 || equipment.EquippedCount(item) > 0 || InStorage(item)) continue;
                (gone ??= new List<EquipmentData>()).Add(item);
            }
            if (gone == null) return;
            foreach (var item in gone) wear.Remove(item);
            Changed?.Invoke();
        }

        static bool InStorage(EquipmentData item)
        {
            foreach (var interactable in Interactable.Active)
                if (interactable is StorageChest chest && chest.Contents != null && chest.Contents.CountOf(item) > 0) return true;
            return false;
        }

        // ---------- Save ----------

        public string CaptureState()
        {
            var state = new State { items = new string[wear.Count], wear = new float[wear.Count] };
            int i = 0;
            foreach (var pair in wear)
            {
                state.items[i] = pair.Key != null ? pair.Key.Id : string.Empty;
                state.wear[i] = pair.Value;
                i++;
            }
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            wear.Clear();
            var state = JsonUtility.FromJson<State>(json);
            if (state?.items != null && Services.TryGet(out GameDatabase database))
                for (int i = 0; i < state.items.Length && i < state.wear.Length; i++)
                    if (database.TryGet(state.items[i], out EquipmentData item)) wear[item] = state.wear[i];
            if (stats != null) stats.MarkDirty();
            Changed?.Invoke();
        }
    }
}
