using System;
using Beast.Core;
using UnityEngine;

namespace Beast.Gameplay
{
    /// <summary>
    /// A merchant's stock and pricing rules. Prices derive from each item's BaseValue:
    /// the player buys at BaseValue × SellMarkup and sells at BaseValue × BuyRate × demand.
    /// Selling many of one item saturates this market; demand recovers each day.
    /// </summary>
    [CreateAssetMenu(menuName = "Beast/Economy/Shop", fileName = "Shop")]
    public sealed class ShopData : GameData
    {
        [Serializable]
        public sealed class StockEntry
        {
            public ItemData Item;
            [Tooltip("Units available per day. -1 = unlimited.")]
            public int DailyStock = 10;
        }

        public string MerchantName = "Merchant";
        [TextArea] public string Greeting;

        [Header("Stock (restocks every morning)")]
        public StockEntry[] Stock;

        [Header("Prices")]
        [Tooltip("The player pays BaseValue × this when buying.")]
        public float SellMarkup = 1.25f;
        [Tooltip("The player receives BaseValue × this (× demand) when selling.")]
        public float BuyRate = 0.5f;
        [Tooltip("Item categories this merchant will buy from the player. Quest items are never bought.")]
        public ItemCategory[] Buys = { ItemCategory.Material, ItemCategory.Crop, ItemCategory.Consumable, ItemCategory.Seed };

        [Header("Supply & Demand")]
        [Tooltip("How much each unit sold lowers what this merchant pays for that item (0.04 = 4%).")]
        public float SaturationPerUnit = 0.04f;
        [Tooltip("Demand never drops below this fraction of the normal price.")]
        [Range(0f, 1f)] public float MinDemand = 0.4f;
        [Tooltip("Fraction of saturation that recovers each day.")]
        [Range(0f, 1f)] public float DailyRecovery = 0.35f;
    }
}
