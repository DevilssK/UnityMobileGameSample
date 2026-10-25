#nullable enable

using UnityEngine.Purchasing;

namespace DevilCorp.MobileFoundation.Purchases.UnityIap
{
    /// <summary>
    /// A product sold by the game: catalog ID used by the game and backend, and its ID on each store.
    /// </summary>
    public sealed class StoreProductDefinition
    {
        public StoreProductDefinition(string productId, ProductType type, string googlePlayId, string appleAppStoreId)
        {
            ProductId = productId;
            Type = type;
            GooglePlayId = googlePlayId;
            AppleAppStoreId = appleAppStoreId;
        }

        public string ProductId { get; }
        public ProductType Type { get; }
        public string GooglePlayId { get; }
        public string AppleAppStoreId { get; }
    }
}
