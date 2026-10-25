using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using GemHunterUGSCloud.Game;
using Xunit;

namespace GemHunterUGSCloud.Tests.Game;

public class PurchaseCatalogFileTests
{
    private static string RepoFile(string relative, [CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "../../..", relative));

    [Fact]
    public void Load_EmbeddedCatalog_HasTheSevenProducts()
    {
        var catalog = GemHunterPurchaseCatalog.Load();

        Assert.Equal(7, catalog.Products.Count);
    }

    [Fact]
    public void AllClientProductIds_ExistInCatalog()
    {
        var clientSource = File.ReadAllText(RepoFile("Assets/GemHunterUGS/Scripts/Store/GemHunterProductCatalog.cs"));
        var clientIds = Regex.Matches(clientSource, "\"([A-Z][A-Z0-9_]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        var catalog = GemHunterPurchaseCatalog.Load();

        Assert.Equal(7, clientIds.Count);
        foreach (var id in clientIds)
        {
            var product = catalog.FindByCatalogId(id);
            Assert.True(product != null, $"{id} missing from purchase_catalog.json");
            // GemHunterProductCatalog builds store ids as <lowercase id>_play / _apple.
            Assert.Equal(id.ToLowerInvariant() + "_play", product!.StoreIds.Google);
            Assert.Equal(id.ToLowerInvariant() + "_apple", product.StoreIds.Apple);
        }
    }
}
