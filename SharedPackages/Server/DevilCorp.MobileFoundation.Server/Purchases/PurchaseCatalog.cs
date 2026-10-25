using System;
using System.Collections.Generic;
using System.Linq;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DevilCorp.MobileFoundation.Server.Purchases;

/// <summary>Store names as they appear in the Unity IAP receipt envelope.</summary>
public static class PurchaseStores
{
    public const string GooglePlay = "GooglePlay";
    public const string AppleAppStore = "AppleAppStore";
}

/// <summary>
/// The server's authority on what each paid product grants. A grant is always looked up from the product
/// id of a verified receipt, never from an id the client chose.
/// </summary>
public sealed class PurchaseCatalog
{
    [JsonProperty("products")]
    public List<PurchaseProduct> Products { get; set; } = new();

    public PurchaseProduct? FindByCatalogId(string catalogId) =>
        Products.FirstOrDefault(p => string.Equals(p.CatalogId, catalogId, StringComparison.Ordinal));

    public PurchaseProduct? FindByStoreProductId(string store, string storeProductId) => store switch
    {
        PurchaseStores.GooglePlay => Products.FirstOrDefault(p => string.Equals(p.StoreIds.Google, storeProductId, StringComparison.Ordinal)),
        PurchaseStores.AppleAppStore => Products.FirstOrDefault(p => string.Equals(p.StoreIds.Apple, storeProductId, StringComparison.Ordinal)),
        _ => null,
    };

    public static PurchaseCatalog Parse(JToken json)
    {
        PurchaseCatalog? catalog;
        try
        {
            catalog = json.ToObject<PurchaseCatalog>();
        }
        catch (JsonException e)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, $"invalid purchase catalog: {e.Message}");
        }

        if (catalog == null || catalog.Products.Any(p => string.IsNullOrEmpty(p.CatalogId) || p.Grants.Count == 0
                || p.Grants.Any(g => g.Amount <= 0)))
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, "invalid purchase catalog values");
        }

        if (catalog.Products.Select(p => p.CatalogId).Distinct(StringComparer.Ordinal).Count() != catalog.Products.Count)
        {
            throw new GameOperationException(ErrorCodes.ConfigurationError, "a catalog id is declared twice");
        }

        return catalog;
    }
}

public sealed class PurchaseProduct
{
    [JsonProperty("catalogId")]
    public string CatalogId { get; set; } = "";

    /// <summary>Consumable or NonConsumable, as in Unity IAP.</summary>
    [JsonProperty("type")]
    public string Type { get; set; } = "";

    [JsonProperty("storeIds")]
    public StoreProductIds StoreIds { get; set; } = new();

    [JsonProperty("grants")]
    public List<Grant> Grants { get; set; } = new();
}

public sealed class StoreProductIds
{
    [JsonProperty("google")]
    public string? Google { get; set; }

    [JsonProperty("apple")]
    public string? Apple { get; set; }
}
