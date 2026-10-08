using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Slot-based bag with stacking, plus a gold wallet. Saves item Ids (looked up in the GameDatabase on load).
    /// Also used for storage (the homestead chest): there, adding items is silent and the save summary leaves it out.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Inventory : MonoBehaviour, ISaveable, ISaveSummarySource
    {
        [Serializable]
        public struct StartingItem
        {
            public ItemData Item;
            [Min(1)] public int Count;
        }

        [SerializeField, Min(1)] int slotCount = 30;
        [SerializeField, Tooltip("Unique, stable ID used in save files.")] string saveId = "player.inventory";
        [SerializeField] StartingItem[] startingItems;
        [SerializeField, Min(0)] int startingGold;
        [SerializeField, Tooltip("Show \"+N item\" notices when items are added (the player's bag). Off for storage.")]
        bool announceAdds = true;
        [SerializeField, Tooltip("List this inventory's gold in save-slot summaries (the player's bag only).")]
        bool describeInSave = true;

        ItemStack[] slots;

        public int Gold { get; private set; }
        public int SlotCount => slots.Length;
        public ItemStack this[int index] => slots[index];
        public string SaveId => saveId;

        /// <summary>Raised after any change to items or gold (for UI refresh).</summary>
        public event Action Changed;

        [Serializable]
        sealed class State
        {
            public int gold;
            public string[] itemIds;
            public int[] counts;
        }

        void Awake()
        {
            slots = new ItemStack[slotCount];
            Gold = startingGold;
            if (startingItems == null) return;
            foreach (var start in startingItems) AddInternal(start.Item, start.Count, raiseEvent: false);
        }

        void OnEnable()
        {
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        void OnDisable()
        {
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        // ---------- Items ----------

        /// <summary>More slots (never fewer): the Larger Chest upgrade. Items stay where they are.</summary>
        public void Grow(int newSlotCount)
        {
            if (slots == null || newSlotCount <= slots.Length) return;
            Array.Resize(ref slots, newSlotCount);
            Changed?.Invoke();
        }

        /// <summary>Adds as many as fit. Returns how many did NOT fit.</summary>
        public int Add(ItemData item, int count) => AddInternal(item, count, raiseEvent: announceAdds);

        /// <summary>Adds without a "+N item" notice (moving things between bag and storage). Returns how many did NOT fit.</summary>
        public int AddQuietly(ItemData item, int count) => AddInternal(item, count, raiseEvent: false);

        /// <summary>Slots holding something.</summary>
        public int UsedSlots
        {
            get
            {
                int used = 0;
                foreach (var slot in slots) if (!slot.IsEmpty) used++;
                return used;
            }
        }

        public bool Remove(ItemData item, int count)
        {
            if (item == null || count <= 0 || CountOf(item) < count) return false;

            // Take from the last stacks first so the front of the bag stays stable.
            for (int i = slots.Length - 1; i >= 0 && count > 0; i--)
            {
                if (slots[i].IsEmpty || slots[i].Item != item) continue;
                int taken = Mathf.Min(count, slots[i].Count);
                count -= taken;
                int left = slots[i].Count - taken;
                slots[i] = left > 0 ? new ItemStack(item, left) : default;
            }
            Changed?.Invoke();
            return true;
        }

        public int CountOf(ItemData item)
        {
            int total = 0;
            foreach (var slot in slots)
                if (!slot.IsEmpty && slot.Item == item) total += slot.Count;
            return total;
        }

        /// <summary>How many more of this item fit (existing stacks + empty slots).</summary>
        public int SpaceFor(ItemData item)
        {
            if (item == null) return 0;
            int space = 0;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty) space += item.MaxStack;
                else if (slot.Item == item) space += item.MaxStack - slot.Count;
            }
            return space;
        }

        /// <summary>First item of a given type, e.g. FindFirst&lt;ConsumableData&gt;() for the quick-use slot.</summary>
        public T FindFirst<T>() where T : ItemData
        {
            foreach (var slot in slots)
                if (!slot.IsEmpty && slot.Item is T match) return match;
            return null;
        }

        int AddInternal(ItemData item, int count, bool raiseEvent)
        {
            if (item == null || count <= 0) return count;

            int remaining = count;
            for (int i = 0; i < slots.Length && remaining > 0; i++)
                if (!slots[i].IsEmpty && slots[i].Item == item) remaining = FillSlot(i, item, remaining);
            for (int i = 0; i < slots.Length && remaining > 0; i++)
                if (slots[i].IsEmpty) remaining = FillSlot(i, item, remaining);

            int added = count - remaining;
            if (added > 0)
            {
                Changed?.Invoke();
                if (raiseEvent) EventBus<ItemsAddedEvent>.Raise(new ItemsAddedEvent(item, added));
            }
            return remaining;
        }

        int FillSlot(int index, ItemData item, int amount)
        {
            int current = slots[index].IsEmpty ? 0 : slots[index].Count;
            int moved = Mathf.Min(item.MaxStack - current, amount);
            if (moved <= 0) return amount;
            slots[index] = new ItemStack(item, current + moved);
            return amount - moved;
        }

        // ---------- Gold ----------

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            Changed?.Invoke();
            EventBus<GoldChangedEvent>.Raise(new GoldChangedEvent(amount, Gold));
        }

        public bool TrySpendGold(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            Changed?.Invoke();
            EventBus<GoldChangedEvent>.Raise(new GoldChangedEvent(-amount, Gold));
            return true;
        }

        // ---------- Save ----------

        public void Describe(SaveSummary summary)
        {
            if (describeInSave) summary.details.Add($"{Gold} gold");
        }

        public string CaptureState()
        {
            var state = new State { gold = Gold, itemIds = new string[slots.Length], counts = new int[slots.Length] };
            for (int i = 0; i < slots.Length; i++)
            {
                state.itemIds[i] = slots[i].IsEmpty ? string.Empty : slots[i].Item.Id;
                state.counts[i] = slots[i].IsEmpty ? 0 : slots[i].Count;
            }
            return JsonUtility.ToJson(state);
        }

        public void RestoreState(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            if (!Services.TryGet(out GameDatabase database))
            {
                Debug.LogError("[Inventory] No GameDatabase registered; can't restore items.");
                return;
            }

            // A chest saved after it was enlarged comes back at its saved size.
            if (state.itemIds != null && state.itemIds.Length > slots.Length) Array.Resize(ref slots, state.itemIds.Length);
            Array.Clear(slots, 0, slots.Length);
            int count = Mathf.Min(slots.Length, state.itemIds?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                string id = state.itemIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (database.TryGet(id, out ItemData item)) slots[i] = new ItemStack(item, state.counts[i]);
                else Debug.LogWarning($"[Inventory] Saved item '{id}' no longer exists; dropped.");
            }
            Gold = state.gold;
            Changed?.Invoke();
        }
    }
}
