using System.IO;
using DevilCorp.MobileFoundation.Server.Purchases;
using Newtonsoft.Json.Linq;

namespace GemHunterUGSCloud.Game;

/// <summary>purchase_catalog.json, embedded in the module: deployed with the code that relies on it.</summary>
public static class GemHunterPurchaseCatalog
{
    private const string ResourceName = "GemHunterUGSCloud.purchase_catalog.json";

    public static PurchaseCatalog Load()
    {
        using var stream = typeof(GemHunterPurchaseCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new FileNotFoundException($"Embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return PurchaseCatalog.Parse(JToken.Parse(reader.ReadToEnd()));
    }
}
