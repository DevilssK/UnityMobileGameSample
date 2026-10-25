using System.Collections.Generic;
using DevilCorp.MobileFoundation.Purchases.UnityIap;
using UnityEngine.Purchasing;

namespace GemHunterUGS.Scripts.Store
{
    /// <summary>
    /// Real-money products sold by Gem Hunter. Product IDs match the Economy real-money purchases (.ecr);
    /// store IDs match the storeIdentifiers declared in those .ecr files.
    /// </summary>
    public static class GemHunterProductCatalog
    {
        public const string k_BundlePack = "BUNDLE_PACK";
        public const string k_MegaPack = "MEGA_PACK";

        public static readonly IReadOnlyDictionary<int, string> CoinPacks = new Dictionary<int, string>
        {
            { 100, "COIN_PACK_100" },
            { 500, "COIN_PACK_500" },
            { 1000, "COIN_PACK_1000" },
            { 5000, "COIN_PACK_5000" },
            { 10000, "COIN_PACK_10000" }
        };

        public static IReadOnlyList<StoreProductDefinition> All { get; } = Build();

        private static IReadOnlyList<StoreProductDefinition> Build()
        {
            var products = new List<StoreProductDefinition>
            {
                Consumable(k_BundlePack, "bundle_pack"),
                Consumable(k_MegaPack, "mega_pack")
            };

            foreach (var coinPack in CoinPacks)
            {
                products.Add(Consumable(coinPack.Value, $"coin_pack_{coinPack.Key}"));
            }

            return products;
        }

        private static StoreProductDefinition Consumable(string productId, string storeIdPrefix) =>
            new StoreProductDefinition(productId, ProductType.Consumable, $"{storeIdPrefix}_play", $"{storeIdPrefix}_apple");
    }
}
