using Beast.Core;

namespace Beast.Gameplay
{
    /// <summary>An item and how many of it. The default value is an empty slot.</summary>
    public readonly struct ItemStack
    {
        public readonly ItemData Item;
        public readonly int Count;

        public ItemStack(ItemData item, int count)
        {
            Item = item;
            Count = count;
        }

        public bool IsEmpty => Item == null || Count <= 0;
    }

    /// <summary>Raised when items enter the player's inventory (pickups, rewards, harvests). Quests will listen.</summary>
    public readonly struct ItemsAddedEvent : IEvent
    {
        public readonly ItemData Item;
        public readonly int Count;
        public ItemsAddedEvent(ItemData item, int count) { Item = item; Count = count; }
    }

    public readonly struct GoldChangedEvent : IEvent
    {
        public readonly int Delta;
        public readonly int Total;
        public GoldChangedEvent(int delta, int total) { Delta = delta; Total = total; }
    }

    public readonly struct ItemUsedEvent : IEvent
    {
        public readonly ItemData Item;
        public ItemUsedEvent(ItemData item) { Item = item; }
    }
}
