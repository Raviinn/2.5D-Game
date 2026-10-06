using System;
using System.Collections.Generic;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// A merchant in the world: opens the trade screen, prices items, restocks each morning,
    /// tracks market saturation (supply &amp; demand), and saves its state.
    /// Trade logic lives here, not in the UI, so later UIs and systems reuse it.
    /// </summary>
    public sealed class Shopkeeper : Interactable, ISaveable
    {
        [SerializeField] ShopData shop;
        [SerializeField] float range = 2.5f;
        [SerializeField, Tooltip("Off when the NPC has dialogue: the conversation opens the shop via open_shop().")]
        bool directInteraction = true;

        int[] stock;
        readonly Dictionary<ItemData, float> saturation = new(); // units sold here recently, per item
        readonly List<ItemData> recoveryBuffer = new();
        GameStateService state;

        [Serializable]
        sealed class State
        {
            public int[] stock;
            public string[] saturatedItems;
            public float[] saturationValues;
        }

        public ShopData Shop => shop;
        public string SaveId => shop != null ? $"shop.{shop.Id}" : "shop.unassigned";

        /// <summary>Scales prices by the player's standing with the town (below 1 = better prices both ways). 1 = neutral.</summary>
        public float PriceModifier => Services.TryGet(out Reputation reputation) ? reputation.PriceModifier : 1f;

        void Awake()
        {
            if (shop == null || shop.Stock == null)
            {
                Debug.LogError($"[Shop] {name} has no ShopData.", this);
                enabled = false;
                return;
            }
            stock = new int[shop.Stock.Length];
            Restock();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EventBus<DayPassedEvent>.Subscribe(OnDayPassed);
            if (Services.TryGet(out SaveService save)) save.Register(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EventBus<DayPassedEvent>.Unsubscribe(OnDayPassed);
            if (Services.TryGet(out SaveService save)) save.Unregister(this);
        }

        void Start() => state = Services.Get<GameStateService>();

        // ---------- Interaction ----------

        public override bool TryGetPrompt(PlayerInteractor interactor, out InteractionPrompt prompt)
        {
            prompt = default;
            if (!directInteraction) return false;
            float distance = Vector3.Distance(interactor.transform.position, transform.position);
            if (distance > range) return false;

            prompt.Text = $"Trade with {shop.MerchantName}";
            prompt.CanInteract = true;
            prompt.Distance = distance;
            return true;
        }

        public override void Interact(PlayerInteractor interactor) => Open();

        /// <summary>Opens the trade screen (from interaction, or from dialogue).</summary>
        public void Open()
        {
            if (shop == null) return; // misconfigured (already logged in Awake)
            state ??= Services.Get<GameStateService>();
            state.SetState(GameState.InGameMenu);
            EventBus<ShopOpenedEvent>.Raise(new ShopOpenedEvent(this));
        }

        // ---------- Pricing ----------

        public int StockCount => shop.Stock.Length;
        public ItemData StockItem(int index) => shop.Stock[index].Item;
        public bool IsUnlimited(int index) => shop.Stock[index].DailyStock < 0;
        public int StockLeft(int index) => IsUnlimited(index) ? int.MaxValue : stock[index];

        public bool Buys(ItemData item) =>
            item != null && item.Category != ItemCategory.Quest && Array.IndexOf(shop.Buys, item.Category) >= 0;

        /// <summary>What the player pays for one unit.</summary>
        public int BuyPrice(ItemData item) => Mathf.Max(1, Mathf.CeilToInt(item.BaseValue * shop.SellMarkup * PriceModifier));

        /// <summary>What the player receives for one unit right now (drops as this market saturates).</summary>
        public int SellPrice(ItemData item)
        {
            if (item.BaseValue <= 0) return 0;
            return Mathf.Max(1, Mathf.FloorToInt(item.BaseValue * shop.BuyRate * Demand(item) / PriceModifier));
        }

        /// <summary>1 = normal demand; lower once the player has sold a lot of this item here.</summary>
        public float Demand(ItemData item) =>
            saturation.TryGetValue(item, out float units) ? Mathf.Max(shop.MinDemand, 1f - units * shop.SaturationPerUnit) : 1f;

        // ---------- Trading ----------

        public bool TryBuy(Inventory inventory, int index, out string failure)
        {
            var item = StockItem(index);
            int price = BuyPrice(item);

            failure = StockLeft(index) <= 0 ? "Sold out for today"
                : inventory.Gold < price ? "Not enough gold"
                : inventory.SpaceFor(item) <= 0 ? "Bag is full"
                : null;
            if (failure != null) return false;

            inventory.TrySpendGold(price);
            inventory.Add(item, 1);
            if (!IsUnlimited(index)) stock[index]--;
            EventBus<ItemTradedEvent>.Raise(new ItemTradedEvent(shop, item, 1, price, playerBought: true));
            return true;
        }

        /// <summary>Sells up to 'amount' units, re-pricing each unit as demand falls. Returns gold earned.</summary>
        public int Sell(Inventory inventory, ItemData item, int amount)
        {
            if (!Buys(item)) return 0;
            amount = Mathf.Min(amount, inventory.CountOf(item));
            if (amount <= 0) return 0;

            int earned = 0;
            for (int i = 0; i < amount; i++)
            {
                earned += SellPrice(item);
                saturation[item] = (saturation.TryGetValue(item, out float units) ? units : 0f) + 1f;
            }
            inventory.Remove(item, amount);
            inventory.AddGold(earned);
            EventBus<ItemTradedEvent>.Raise(new ItemTradedEvent(shop, item, amount, earned, playerBought: false));
            return earned;
        }

        // ---------- Daily restock ----------

        void OnDayPassed(DayPassedEvent evt)
        {
            Restock();

            // Markets recover a fraction of their saturation each day.
            recoveryBuffer.Clear();
            recoveryBuffer.AddRange(saturation.Keys);
            foreach (var item in recoveryBuffer)
            {
                float remaining = saturation[item] * (1f - shop.DailyRecovery);
                if (remaining < 0.5f) saturation.Remove(item);
                else saturation[item] = remaining;
            }
        }

        void Restock()
        {
            for (int i = 0; i < stock.Length; i++) stock[i] = Mathf.Max(0, shop.Stock[i].DailyStock);
        }

        // ---------- Save ----------

        public string CaptureState()
        {
            var saved = new State
            {
                stock = (int[])stock.Clone(),
                saturatedItems = new string[saturation.Count],
                saturationValues = new float[saturation.Count],
            };
            int i = 0;
            foreach (var pair in saturation)
            {
                saved.saturatedItems[i] = pair.Key.Id;
                saved.saturationValues[i] = pair.Value;
                i++;
            }
            return JsonUtility.ToJson(saved);
        }

        public void RestoreState(string json)
        {
            var saved = JsonUtility.FromJson<State>(json);
            if (saved.stock != null)
                for (int i = 0; i < Mathf.Min(stock.Length, saved.stock.Length); i++) stock[i] = saved.stock[i];

            saturation.Clear();
            if (saved.saturatedItems == null || !Services.TryGet(out GameDatabase database)) return;
            for (int i = 0; i < saved.saturatedItems.Length; i++)
                if (database.TryGet(saved.saturatedItems[i], out ItemData item)) saturation[item] = saved.saturationValues[i];
        }
    }
}
