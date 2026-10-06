using Beast.Core;

namespace Beast.Gameplay
{
    public readonly struct ShopOpenedEvent : IEvent
    {
        public readonly Shopkeeper Shop;
        public ShopOpenedEvent(Shopkeeper shop) { Shop = shop; }
    }

    /// <summary>Every completed trade. Reputation listens (trade raises standing); quests and stats may later.</summary>
    public readonly struct ItemTradedEvent : IEvent
    {
        public readonly ShopData Shop;
        public readonly ItemData Item;
        public readonly int Count;
        public readonly int Gold;
        public readonly bool PlayerBought;

        public ItemTradedEvent(ShopData shop, ItemData item, int count, int gold, bool playerBought)
        {
            Shop = shop; Item = item; Count = count; Gold = gold; PlayerBought = playerBought;
        }
    }
}
