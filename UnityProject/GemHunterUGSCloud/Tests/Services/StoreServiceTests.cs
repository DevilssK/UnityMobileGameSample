using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.Purchases;
using GemHunterUGSCloud.Game;
using GemHunterUGSCloud.Services;
using GemHunterUGSCloud.Tests.Support;
using Xunit;

namespace GemHunterUGSCloud.Tests.Services;

public class StoreServiceTests
{
    private const string GooglePayload = "{\"json\":\"{\\\"orderId\\\":\\\"GPA.1\\\"}\",\"signature\":\"sig==\"}";

    private readonly GameTestHarness _harness = new();
    private readonly StoreService _service;

    public StoreServiceTests()
    {
        _service = new StoreService(GameTestHarness.Logger<StoreService>(), _harness.State, GemHunterPurchaseCatalog.Load());
    }

    private static string Receipt(string store, string transactionId, string? payload = "fake-payload")
    {
        var payloadJson = payload == null ? "null" : Newtonsoft.Json.JsonConvert.ToString(payload);
        return $"{{\"Store\":\"{store}\",\"TransactionID\":\"{transactionId}\",\"Payload\":{payloadJson}}}";
    }

    [Fact]
    public async Task HandlePurchase_FakeStoreInDevelopment_DeliversOnce()
    {
        var context = await _harness.NewPlayerAsync();

        var first = await _service.HandlePurchase(context, "COIN_PACK_100", Receipt("fake", "fake-1"), "fake-1");
        var replay = await _service.HandlePurchase(context, "COIN_PACK_100", Receipt("fake", "fake-1"), "fake-1");

        Assert.Equal(300, first.Currencies["COIN"]);
        Assert.Equal(300, replay.Currencies["COIN"]);
        Assert.NotNull(_harness.Store.Get(PurchaseLedger.KeyFor("fake-1")));
    }

    [Fact]
    public async Task HandlePurchase_BundleWithInfiniteHeart_GrantsAllRewards()
    {
        var context = await _harness.NewPlayerAsync();

        var economy = await _service.HandlePurchase(context, "BUNDLE_PACK", Receipt("fake", "fake-2"), "fake-2");

        Assert.Equal(250, economy.Currencies["COIN"]);
        Assert.Equal(3, economy.ItemInventory["LARGE_BOMB"]);
        Assert.Equal(4, economy.ItemInventory["COLOR_BONUS"]);
        Assert.Equal(_harness.Clock.UtcNowUnixMs / 1000 + 360, economy.InfiniteHeartsExpiryTimestamp);
    }

    [Fact]
    public async Task HandlePurchase_FakeStoreInProduction_IsRejected()
    {
        var context = await _harness.NewPlayerAsync("production");

        var error = await Assert.ThrowsAnyAsync<System.Exception>(() =>
            _service.HandlePurchase(context, "COIN_PACK_100", Receipt("fake", "fake-1"), "fake-1"));

        Assert.StartsWith("PURCHASE_REJECTED", error.Message);
        Assert.Null(_harness.Store.Get(PurchaseLedger.KeyFor("fake-1")));
    }

    [Fact]
    public async Task HandlePurchase_RealGoogleReceipt_RetriesLaterWithoutGrant()
    {
        var context = await _harness.NewPlayerAsync();

        var error = await Assert.ThrowsAnyAsync<System.Exception>(() =>
            _service.HandlePurchase(context, "COIN_PACK_100", Receipt("GooglePlay", "GPA.1", GooglePayload), "GPA.1"));

        Assert.StartsWith("PURCHASE_RETRY_LATER", error.Message);
        Assert.Null(_harness.Store.Get(PurchaseLedger.KeyFor("GPA.1")));
    }

    [Fact]
    public async Task HandlePurchase_UnreadableReceipt_IsRejected()
    {
        var context = await _harness.NewPlayerAsync();

        var error = await Assert.ThrowsAnyAsync<System.Exception>(() =>
            _service.HandlePurchase(context, "COIN_PACK_100", "not json", "x"));

        Assert.StartsWith("PURCHASE_REJECTED", error.Message);
    }

    [Fact]
    public async Task HandlePurchase_UnknownProduct_IsRejected()
    {
        var context = await _harness.NewPlayerAsync();

        var error = await Assert.ThrowsAnyAsync<System.Exception>(() =>
            _service.HandlePurchase(context, "GEM_PACK", Receipt("fake", "fake-3"), "fake-3"));

        Assert.StartsWith("PURCHASE_REJECTED", error.Message);
    }

    [Fact]
    public async Task HandlePurchase_ReplayAfterProductRemovedFromCatalog_ReturnsDeliveredState()
    {
        var context = await _harness.NewPlayerAsync();
        await _service.HandlePurchase(context, "COIN_PACK_100", Receipt("fake", "fake-5"), "fake-5");
        var withoutProduct = new StoreService(GameTestHarness.Logger<StoreService>(), _harness.State,
            PurchaseCatalog.Parse(Newtonsoft.Json.Linq.JToken.Parse("{\"products\":[]}")));

        var replay = await withoutProduct.HandlePurchase(context, "COIN_PACK_100", Receipt("fake", "fake-5"), "fake-5");

        Assert.Equal(300, replay.Currencies["COIN"]);
    }

    [Fact]
    public async Task HandlePurchase_DuringCreationGracePeriod_RetriesLater()
    {
        var context = GameTestHarness.Context();

        var error = await Assert.ThrowsAnyAsync<System.Exception>(() =>
            _service.HandlePurchase(context, "COIN_PACK_100", Receipt("fake", "fake-4"), "fake-4"));

        Assert.StartsWith("PURCHASE_RETRY_LATER", error.Message);
        Assert.Null(_harness.Store.Get(PurchaseLedger.KeyFor("fake-4")));
    }

    [Fact]
    public async Task HandleFreeCoinPackPurchase_Twice_GrantsOnce()
    {
        var context = await _harness.NewPlayerAsync();

        await _service.HandleFreeCoinPackPurchase(context);
        var second = await _service.HandleFreeCoinPackPurchase(context);

        Assert.Equal(210, second.Currencies["COIN"]);
        Assert.True(second.HasPurchasedFreeCoinPack);
    }
}
