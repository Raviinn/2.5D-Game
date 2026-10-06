using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// Prototype trade UI (IMGUI): merchant stock on the left, the player's sellable items on the right.
    /// Opened by a Shopkeeper; the game is paused (InGameMenu) while it's open. Every trade reports what happened.
    /// </summary>
    public sealed class ShopScreen : MonoBehaviour
    {
        const float RowHeight = 62f;
        const float MessageLifetime = 3f;

        Shopkeeper shop;
        Inventory inventory;
        GameStateService state;
        Vector2 buyScroll;
        Vector2 sellScroll;
        readonly List<ItemData> sellable = new();
        string message;
        bool messageIsError;
        float messageTime;

        void OnEnable()
        {
            EventBus<ShopOpenedEvent>.Subscribe(OnShopOpened);
            EventBus<GameStateChangedEvent>.Subscribe(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus<ShopOpenedEvent>.Unsubscribe(OnShopOpened);
            EventBus<GameStateChangedEvent>.Unsubscribe(OnGameStateChanged);
        }

        void Start()
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) inventory = player.GetComponent<Inventory>();
            state = Services.Get<GameStateService>();
        }

        void OnShopOpened(ShopOpenedEvent evt)
        {
            shop = evt.Shop;
            message = null;
            buyScroll = sellScroll = Vector2.zero;
        }

        void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (evt.Current != GameState.InGameMenu) shop = null;
        }

        void OnGUI()
        {
            if (shop == null || inventory == null || state.Current != GameState.InGameMenu) return;
            UITheme.Begin();

            var data = shop.Shop;
            string footer = message != null && Time.unscaledTime - messageTime < MessageLifetime
                ? $"<color={(messageIsError ? UITheme.BadHex : UITheme.GoodHex)}>{message}</color>"
                : $"<color={UITheme.MutedHex}>Prices drop as you sell the same goods here; markets recover each day.   ·   Tab / Esc: leave</color>";
            var area = UITheme.Window(1060f, 660f, data.MerchantName, $"<color={UITheme.GoldHex}>Your gold: {inventory.Gold}</color>{StandingText()}", footer);

            if (!string.IsNullOrEmpty(data.Greeting))
                GUI.Label(new Rect(area.x + 4f, area.y, area.width - 8f, 24f), $"<i><color={UITheme.MutedHex}>“{data.Greeting}”</color></i>", UITheme.Body);

            float top = area.y + 34f;
            float columnWidth = (area.width - 20f) * 0.5f;
            DrawBuyColumn(new Rect(area.x, top, columnWidth, area.yMax - top));
            DrawSellColumn(new Rect(area.x + columnWidth + 20f, top, columnWidth, area.yMax - top));
        }

        void DrawBuyColumn(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 26f), "Buy", UITheme.Header);
            var view = new Rect(area.x, area.y + 30f, area.width, area.height - 30f);
            UITheme.Inset(view);
            var content = new Rect(0f, 0f, view.width - 20f, Mathf.Max(1, shop.StockCount) * RowHeight + 8f);
            buyScroll = GUI.BeginScrollView(new Rect(view.x + 4f, view.y + 4f, view.width - 8f, view.height - 8f), buyScroll, content);

            for (int i = 0; i < shop.StockCount; i++)
            {
                var item = shop.StockItem(i);
                if (item == null) continue;
                var row = new Rect(4f, 4f + i * RowHeight, content.width - 8f, RowHeight - 6f);
                int left = shop.StockLeft(i);
                int price = shop.BuyPrice(item);
                bool affordable = inventory.Gold >= price;
                string stockText = shop.IsUnlimited(i) ? "plenty" : left > 0 ? $"{left} left today" : $"<color={UITheme.BadHex}>sold out</color>";
                string priceText = $"<color={(affordable ? UITheme.GoldHex : UITheme.BadHex)}>{price}g</color>";

                DrawRowItem(row, item, $"<b>{item.DisplayName}</b>\n<size=15>{priceText}  <color={UITheme.MutedHex}>·  {stockText}  ·  you have {inventory.CountOf(item)}</color></size>");

                bool canBuy = left > 0 && affordable;
                if (UITheme.Button(new Rect(row.xMax - 132f, row.y + 12f, 64f, 32f), "Buy", canBuy, primary: true)) Buy(i, 1);
                if (UITheme.Button(new Rect(row.xMax - 62f, row.y + 12f, 56f, 32f), "×5", canBuy)) Buy(i, 5);
            }
            GUI.EndScrollView();
        }

        void Buy(int index, int times)
        {
            var item = shop.StockItem(index);
            int bought = 0, spent = 0;
            string failure = null;
            for (int n = 0; n < times; n++)
            {
                int price = shop.BuyPrice(item);
                if (!shop.TryBuy(inventory, index, out failure)) break;
                bought++;
                spent += price;
            }
            if (bought > 0) Show($"Bought {bought}× {item.DisplayName} for {spent}g." + (failure != null ? $" ({failure})" : string.Empty), false);
            else Show(failure ?? "Can't buy that.", true);
        }

        void DrawSellColumn(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 26f), "Sell", UITheme.Header);

            // Distinct items the merchant will buy, in bag order.
            sellable.Clear();
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var stack = inventory[i];
                if (!stack.IsEmpty && shop.Buys(stack.Item) && !sellable.Contains(stack.Item)) sellable.Add(stack.Item);
            }

            var view = new Rect(area.x, area.y + 30f, area.width, area.height - 30f);
            UITheme.Inset(view);
            var content = new Rect(0f, 0f, view.width - 20f, Mathf.Max(1, sellable.Count) * RowHeight + 8f);
            sellScroll = GUI.BeginScrollView(new Rect(view.x + 4f, view.y + 4f, view.width - 8f, view.height - 8f), sellScroll, content);

            if (sellable.Count == 0)
                GUI.Label(new Rect(12f, 12f, content.width - 24f, 48f), $"<color={UITheme.MutedHex}>You have nothing this merchant wants.</color>", UITheme.Body);

            for (int i = 0; i < sellable.Count; i++)
            {
                var item = sellable[i];
                var row = new Rect(4f, 4f + i * RowHeight, content.width - 8f, RowHeight - 6f);
                int count = inventory.CountOf(item);
                float demand = shop.Demand(item);
                string market = demand < 0.95f ? $"  ·  <color={UITheme.BadHex}>market {Mathf.RoundToInt(demand * 100f)}%</color>" : string.Empty;

                DrawRowItem(row, item, $"<b>{item.DisplayName}</b>  <color={UITheme.MutedHex}>×{count}</color>\n<size=15><color={UITheme.GoldHex}>{shop.SellPrice(item)}g</color> each{market}</size>");

                if (UITheme.Button(new Rect(row.xMax - 132f, row.y + 12f, 64f, 32f), "Sell")) Sell(item, 1);
                if (UITheme.Button(new Rect(row.xMax - 62f, row.y + 12f, 56f, 32f), "All")) Sell(item, count);
            }
            GUI.EndScrollView();
        }

        void Sell(ItemData item, int amount)
        {
            int earned = shop.Sell(inventory, item, amount);
            if (earned > 0) Show($"Sold {amount}× {item.DisplayName} for {earned}g.", false);
            else Show("They won't buy that.", true);
        }

        static void DrawRowItem(Rect row, ItemData item, string text)
        {
            UITheme.Slot(row, false, row.Contains(Event.current.mousePosition));
            UITheme.ItemIcon(new Rect(row.x + 10f, row.y + 10f, 36f, 36f), item);
            GUI.Label(new Rect(row.x + 58f, row.y + 6f, row.width - 200f, row.height - 8f), text, UITheme.Body);
        }

        /// <summary>"   ·   Standing: Trusted (10% better prices)" — why prices are what they are.</summary>
        static string StandingText()
        {
            if (!Services.TryGet(out Reputation reputation) || !reputation.IsConfigured) return string.Empty;
            int bonus = reputation.PriceBonusPercent;
            string effect = bonus > 0 ? $"<color={UITheme.GoodHex}>{bonus}% better prices</color>" : "standard prices";
            return $"<color={UITheme.MutedHex}>   ·   Standing: <b>{reputation.TierName}</b> ({effect})</color>";
        }

        void Show(string text, bool error)
        {
            message = text;
            messageIsError = error;
            messageTime = Time.unscaledTime;
        }
    }
}
