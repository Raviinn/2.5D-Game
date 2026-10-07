using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// The trade window, on parchment: the merchant's stock on the left (Buy / ×5), the player's sellable items on the
    /// right (Sell / All). Opened by a Shopkeeper; the game is paused (InGameMenu) while it's open. Every trade reports
    /// what happened. Tab / Esc leave.
    /// </summary>
    public sealed class ShopScreen : MonoBehaviour
    {
        const float RowHeight = 78f;
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
        GUIStyle rowName, rowDetail, footerStyle, headerRight;

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
            EnsureStyles();

            var data = shop.Shop;
            var area = UITheme.ParchmentWindow(1160f, 760f, data.MerchantName, $"<color={UITheme.InkGoldDarkHex}>Your gold: {inventory.Gold}</color>{StandingText()}");

            float top = area.y;
            if (!string.IsNullOrEmpty(data.Greeting))
            {
                GUI.Label(new Rect(area.x, top, area.width, 26f), $"<i>“{data.Greeting}”</i>", UITheme.PaperMuted);
                top += 36f;
            }

            float footer = 56f;
            float columnWidth = (area.width - 32f) * 0.5f;
            DrawBuyColumn(new Rect(area.x, top, columnWidth, area.yMax - top - footer));
            DrawSellColumn(new Rect(area.x + columnWidth + 32f, top, columnWidth, area.yMax - top - footer));

            string line = message != null && Time.unscaledTime - messageTime < MessageLifetime
                ? $"<color={(messageIsError ? UITheme.VermilionHex : UITheme.GoodOnPaperHex)}>{message}</color>"
                : "Prices drop as you sell the same goods here; markets recover each day.";
            GUI.Label(new Rect(area.x, area.yMax - 34f, area.width - 220f, 30f), line, footerStyle);
            UITheme.KeyHints(area.xMax, area.yMax - 36f, false, ("Esc", "Leave"));
        }

        void DrawBuyColumn(Rect area)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 34f), UITheme.Spaced("Buy"), UITheme.InkHeader);
            var view = new Rect(area.x, area.y + 44f, area.width, area.height - 44f);
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(1, shop.StockCount) * (RowHeight + 8f));
            buyScroll = GUI.BeginScrollView(view, buyScroll, content);

            for (int i = 0; i < shop.StockCount; i++)
            {
                var item = shop.StockItem(i);
                if (item == null) continue;
                var row = new Rect(0f, i * (RowHeight + 8f), content.width, RowHeight);
                int left = shop.StockLeft(i);
                int price = shop.BuyPrice(item);
                bool affordable = inventory.Gold >= price;
                string stockText = shop.IsUnlimited(i) ? "plenty" : left > 0 ? $"{left} left today" : $"<color={UITheme.VermilionHex}>sold out</color>";
                string priceText = $"<color={(affordable ? UITheme.InkGoldDarkHex : UITheme.VermilionHex)}>{price}g</color>";

                DrawRowItem(row, item, item.DisplayName, $"{priceText}  ·  {stockText}  ·  you have {inventory.CountOf(item)}");

                bool canBuy = left > 0 && affordable;
                if (UITheme.BrushButton(new Rect(row.xMax - 196f, row.y + 16f, 110f, 46f), "Buy", canBuy)) Buy(i, 1);
                if (UITheme.BrushButton(new Rect(row.xMax - 78f, row.y + 16f, 66f, 46f), "×5", canBuy)) Buy(i, 5);
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
            GUI.Label(new Rect(area.x, area.y, area.width, 34f), UITheme.Spaced("Sell"), UITheme.InkHeader);

            // Distinct items the merchant will buy, in bag order.
            sellable.Clear();
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var stack = inventory[i];
                if (!stack.IsEmpty && shop.Buys(stack.Item) && !sellable.Contains(stack.Item)) sellable.Add(stack.Item);
            }

            var view = new Rect(area.x, area.y + 44f, area.width, area.height - 44f);
            var content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(1, sellable.Count) * (RowHeight + 8f));
            sellScroll = GUI.BeginScrollView(view, sellScroll, content);

            if (sellable.Count == 0)
                GUI.Label(new Rect(4f, 4f, content.width - 8f, 48f), "You have nothing this merchant wants.", UITheme.PaperMuted);

            for (int i = 0; i < sellable.Count; i++)
            {
                var item = sellable[i];
                var row = new Rect(0f, i * (RowHeight + 8f), content.width, RowHeight);
                int count = inventory.CountOf(item);
                float demand = shop.Demand(item);
                string market = demand < 0.95f ? $"  ·  <color={UITheme.VermilionHex}>market {Mathf.RoundToInt(demand * 100f)}%</color>" : string.Empty;

                DrawRowItem(row, item, $"{item.DisplayName}  <color={UITheme.MutedOnPaperHex}>×{count}</color>",
                    $"<color={UITheme.InkGoldDarkHex}>{shop.SellPrice(item)}g</color> each{market}");

                if (UITheme.BrushButton(new Rect(row.xMax - 196f, row.y + 16f, 110f, 46f), "Sell")) Sell(item, 1);
                if (UITheme.BrushButton(new Rect(row.xMax - 78f, row.y + 16f, 66f, 46f), "All")) Sell(item, count);
            }
            GUI.EndScrollView();
        }

        void Sell(ItemData item, int amount)
        {
            int earned = shop.Sell(inventory, item, amount);
            if (earned > 0) Show($"Sold {amount}× {item.DisplayName} for {earned}g.", false);
            else Show("They won't buy that.", true);
        }

        void DrawRowItem(Rect row, ItemData item, string name, string detail)
        {
            UITheme.PaperSlot(row, false, row.Contains(Event.current.mousePosition));
            UITheme.ItemIcon(new Rect(row.x + 14f, row.y + 14f, 50f, 50f), item);
            GUI.Label(new Rect(row.x + 78f, row.y + 12f, row.width - 290f, 28f), name, rowName);
            GUI.Label(new Rect(row.x + 78f, row.y + 42f, row.width - 290f, 24f), detail, rowDetail);
        }

        /// <summary>"   ·   Standing: Trusted (10% better prices)" — why prices are what they are.</summary>
        static string StandingText()
        {
            if (!Services.TryGet(out Reputation reputation) || !reputation.IsConfigured) return string.Empty;
            int bonus = reputation.PriceBonusPercent;
            string effect = bonus > 0 ? $"<color={UITheme.GoodOnPaperHex}>{bonus}% better prices</color>" : "standard prices";
            return $"   ·   {reputation.TierName} ({effect})";
        }

        void Show(string text, bool error)
        {
            message = text;
            messageIsError = error;
            messageTime = Time.unscaledTime;
        }

        void EnsureStyles()
        {
            if (rowName != null && rowName.font == UITheme.PaperBody.font) return;
            rowName = new GUIStyle(UITheme.PaperBody) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            rowDetail = new GUIStyle(UITheme.PaperMuted) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            footerStyle = new GUIStyle(UITheme.PaperMuted) { wordWrap = false, alignment = TextAnchor.MiddleLeft };
            headerRight = new GUIStyle(UITheme.PaperMuted) { wordWrap = false, alignment = TextAnchor.MiddleRight };
        }
    }
}
