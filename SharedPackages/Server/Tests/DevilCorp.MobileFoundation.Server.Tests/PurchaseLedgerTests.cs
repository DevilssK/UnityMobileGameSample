using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevilCorp.MobileFoundation.Server.Purchases;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Unity.Services.CloudCode.Core;
using Xunit;

namespace DevilCorp.MobileFoundation.Server.Tests;

public class PurchaseLedgerTests
{
    internal static readonly PurchaseCatalog Catalog = PurchaseCatalog.Parse(JToken.Parse(@"{
        ""products"": [
            { ""catalogId"": ""COIN_PACK_100"", ""type"": ""Consumable"",
              ""storeIds"": { ""google"": ""coin_pack_100_play"", ""apple"": ""coin_pack_100_apple"" },
              ""grants"": [ { ""id"": ""COIN"", ""amount"": 100 } ] },
            { ""catalogId"": ""BUNDLE"", ""type"": ""Consumable"",
              ""storeIds"": { ""google"": ""bundle_play"", ""apple"": ""bundle_apple"" },
              ""grants"": [ { ""id"": ""COIN"", ""amount"": 50 }, { ""id"": ""INFINITE_HEART"", ""amount"": 1 } ] }
        ]
    }"));

    private readonly FakeProtectedStateStore _store = new();
    private readonly ManualClock _clock = new();
    private readonly IExecutionContext _context = Substitute.For<IExecutionContext>();

    public PurchaseLedgerTests()
    {
        // Wallet created long ago: outside the grace period.
        _store.Seed(WalletState.Key, WalletRules.Initial(WalletRulesTests.Catalog, _clock.UtcNowUnixMs - 3_600_000));
    }

    private StateTransaction Transaction() => new(_store, _clock, _ => Task.CompletedTask, new ProtectedStateOptions());

    private Task<PurchaseDelivery> Deliver(string transactionId, string catalogId = "COIN_PACK_100", PurchaseCatalog? catalog = null) =>
        PurchaseLedger.DeliverAsync(Transaction(), _context, _clock, (catalog ?? Catalog).FindByCatalogId(catalogId)!,
            PurchaseStores.GooglePlay, transactionId, WalletRulesTests.Catalog, CancellationToken.None);

    private long Coins() => _store.Get(WalletState.Key)!.Value["currencies"]!["COIN"]!.ToObject<long>();

    [Fact]
    public void KeyFor_IsPrefixedLowercaseSha256()
    {
        Assert.Equal("PURCHASE_ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", PurchaseLedger.KeyFor("abc"));
    }

    [Fact]
    public async Task Apply_NewTransaction_CreditsOnceAndWritesRecordAndWallet()
    {
        var delivery = await Deliver("GPA.1");

        Assert.Equal(PurchaseOutcome.Delivered, delivery.Outcome);
        Assert.Equal(300, Coins());
        var batch = Assert.Single(_store.Batches);
        Assert.Equal(new[] { WalletState.Key, PurchaseLedger.KeyFor("GPA.1") }.OrderBy(k => k), batch.Select(w => w.Key).OrderBy(k => k));
        var record = _store.Get(PurchaseLedger.KeyFor("GPA.1"))!.Value;
        Assert.Equal("COIN_PACK_100", record["productId"]!.ToObject<string>());
        Assert.Equal(PurchaseStores.GooglePlay, record["store"]!.ToObject<string>());
        Assert.Equal(1, _store.Get(WalletState.Key)!.Value["purchaseCount"]!.ToObject<int>());
    }

    [Fact]
    public async Task Apply_SameTransactionTwice_SecondIsAlreadyDeliveredWithoutWrite()
    {
        await Deliver("GPA.1");

        var second = await Deliver("GPA.1");

        Assert.Equal(PurchaseOutcome.AlreadyDelivered, second.Outcome);
        Assert.Single(_store.Batches);
        Assert.Equal(300, Coins());
    }

    [Fact]
    public async Task LostResponseThenRetry_PurchaseCreditedOnce()
    {
        // The first call wrote, then its response was lost; the client retries with the same transaction.
        await Deliver("GPA.lost");
        var retry = await Deliver("GPA.lost");

        Assert.Equal(PurchaseOutcome.AlreadyDelivered, retry.Outcome);
        Assert.Equal(300, retry.Wallet.Currencies["COIN"]);
    }

    [Fact]
    public async Task ConcurrentIdenticalPurchases_CreditOnce()
    {
        PurchaseDelivery? concurrent = null;
        _store.BeforeNextWrite = () => concurrent = Deliver("GPA.race").GetAwaiter().GetResult();

        var first = await Deliver("GPA.race");

        Assert.Equal(PurchaseOutcome.Delivered, concurrent!.Outcome);
        Assert.Equal(PurchaseOutcome.AlreadyDelivered, first.Outcome);
        Assert.Equal(300, Coins());
    }

    [Fact]
    public async Task Apply_After250Transactions_ReplayOfFirstGivesNoCredit()
    {
        for (var i = 0; i < 250; i++)
        {
            await Deliver($"GPA.{i}");
        }

        var coinsBefore = Coins();
        var replay = await Deliver("GPA.0");

        Assert.Equal(PurchaseOutcome.AlreadyDelivered, replay.Outcome);
        Assert.Equal(coinsBefore, Coins());
    }

    [Fact]
    public async Task ApplyPurchase_AlreadyRecorded_IgnoresCurrentCatalog()
    {
        await Deliver("GPA.1");
        var changed = PurchaseCatalog.Parse(JToken.Parse(@"{ ""products"": [ { ""catalogId"": ""COIN_PACK_100"", ""type"": ""Consumable"",
            ""storeIds"": { ""google"": ""coin_pack_100_play"" }, ""grants"": [ { ""id"": ""COIN"", ""amount"": 9999 } ] } ] }"));

        var replay = await Deliver("GPA.1", catalog: changed);

        Assert.Equal(PurchaseOutcome.AlreadyDelivered, replay.Outcome);
        Assert.Equal(300, Coins());
    }

    [Fact]
    public async Task Apply_AtMaxRecords_ThrowsConfigurationError()
    {
        var wallet = WalletRules.Initial(WalletRulesTests.Catalog, 0);
        wallet.PurchaseCount = PurchaseLedger.MaxRecords;
        _store.Seed(WalletState.Key, wallet);

        var error = await Assert.ThrowsAsync<GameOperationException>(() => Deliver("GPA.full"));

        Assert.Equal(ErrorCodes.ConfigurationError, error.Code);
        Assert.Empty(_store.Batches);
    }

    [Fact]
    public async Task TwoConcurrentSpendsExceedingBalance_OnlyOneSucceeds()
    {
        Task<long> Spend70() => Transaction().RunAsync(_context, Array.Empty<string>(), snapshot =>
        {
            snapshot.TryGet<WalletState>(WalletState.Key, out var wallet);
            WalletRules.Spend(wallet, "COIN", 170);
            return StateMutation.Write(wallet.Currencies["COIN"], (WalletState.Key, wallet));
        }, CancellationToken.None);

        long? concurrentBalance = null;
        _store.BeforeNextWrite = () => concurrentBalance = Spend70().GetAwaiter().GetResult();

        var error = await Assert.ThrowsAsync<GameOperationException>(Spend70);

        Assert.Equal(30, concurrentBalance);
        Assert.Equal(ErrorCodes.InsufficientFunds, error.Code);
        Assert.Equal(30, Coins());
    }

    [Fact]
    public void FindByStoreProductId_GooglePlayAndApple_ResolveSameProduct()
    {
        var google = Catalog.FindByStoreProductId(PurchaseStores.GooglePlay, "bundle_play");
        var apple = Catalog.FindByStoreProductId(PurchaseStores.AppleAppStore, "bundle_apple");

        Assert.NotNull(google);
        Assert.Same(google, apple);
        Assert.Equal("BUNDLE", google!.CatalogId);
        Assert.Null(Catalog.FindByStoreProductId(PurchaseStores.GooglePlay, "bundle_apple"));
        Assert.Null(Catalog.FindByStoreProductId("fake", "bundle_play"));
    }

    [Fact]
    public void Parse_ProductWithoutGrants_ThrowsConfigurationError()
    {
        var error = Assert.Throws<GameOperationException>(() => PurchaseCatalog.Parse(JToken.Parse(
            @"{ ""products"": [ { ""catalogId"": ""X"", ""type"": ""Consumable"", ""storeIds"": {}, ""grants"": [] } ] }")));

        Assert.Equal(ErrorCodes.ConfigurationError, error.Code);
    }
}
