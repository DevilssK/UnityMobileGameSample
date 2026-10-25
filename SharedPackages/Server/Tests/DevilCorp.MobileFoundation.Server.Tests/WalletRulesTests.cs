using System.Collections.Generic;
using DevilCorp.MobileFoundation.Server.State;
using DevilCorp.MobileFoundation.Server.Wallet;
using Newtonsoft.Json.Linq;
using Xunit;

namespace DevilCorp.MobileFoundation.Server.Tests;

public class WalletRulesTests
{
    internal static readonly WalletCatalog Catalog = WalletCatalog.Parse(JToken.Parse(@"{
        ""currencies"": { ""COIN"": { ""initial"": 200 } },
        ""items"": { ""BOMB"": { ""initial"": 1 }, ""ROCKET"": { ""initial"": 0 } },
        ""timedEffects"": { ""INFINITE_HEART"": { ""durationSeconds"": 360 } }
    }"));

    private const long NowSec = 1_761_400_000;

    [Fact]
    public void Initial_UsesCatalogAndKeepsZeroItems()
    {
        var wallet = WalletRules.Initial(Catalog, 123);

        Assert.Equal(123, wallet.CreatedAtUnixMs);
        Assert.Equal(200, wallet.Currencies["COIN"]);
        Assert.Equal(1, wallet.Items["BOMB"]);
        Assert.Equal(0, wallet.Items["ROCKET"]);
    }

    [Fact]
    public void Credit_CurrencyAndItem_AddsAmounts()
    {
        var wallet = WalletRules.Initial(Catalog, 0);

        WalletRules.Credit(wallet, new[] { new Grant("COIN", 100), new Grant("BOMB", 2) }, Catalog, NowSec);

        Assert.Equal(300, wallet.Currencies["COIN"]);
        Assert.Equal(3, wallet.Items["BOMB"]);
    }

    [Fact]
    public void Credit_TimedEffect_ExtendsFromLaterOfNowAndExpiry()
    {
        var wallet = WalletRules.Initial(Catalog, 0);

        WalletRules.Credit(wallet, new[] { new Grant("INFINITE_HEART", 1) }, Catalog, NowSec);
        Assert.Equal(NowSec + 360, wallet.TimedEffects["INFINITE_HEART"]);

        WalletRules.Credit(wallet, new[] { new Grant("INFINITE_HEART", 2) }, Catalog, NowSec + 10);
        Assert.Equal(NowSec + 360 + 720, wallet.TimedEffects["INFINITE_HEART"]);

        WalletRules.Credit(wallet, new[] { new Grant("INFINITE_HEART", 1) }, Catalog, NowSec + 10_000);
        Assert.Equal(NowSec + 10_000 + 360, wallet.TimedEffects["INFINITE_HEART"]);
    }

    [Fact]
    public void Credit_UnknownId_ThrowsConfigurationError()
    {
        var wallet = WalletRules.Initial(Catalog, 0);

        var error = Assert.Throws<GameOperationException>(() =>
            WalletRules.Credit(wallet, new[] { new Grant("COIN", 5), new Grant("GEM", 1) }, Catalog, NowSec));

        Assert.Equal(ErrorCodes.ConfigurationError, error.Code);
        Assert.Equal(200, wallet.Currencies["COIN"]); // validated before any change
    }

    [Fact]
    public void Credit_NegativeAmount_ThrowsConfigurationError()
    {
        var wallet = WalletRules.Initial(Catalog, 0);

        Assert.Throws<GameOperationException>(() => WalletRules.Credit(wallet, new[] { new Grant("COIN", -5) }, Catalog, NowSec));
    }

    [Fact]
    public void Spend_Enough_Debits()
    {
        var wallet = WalletRules.Initial(Catalog, 0);

        WalletRules.Spend(wallet, "COIN", 150);

        Assert.Equal(50, wallet.Currencies["COIN"]);
    }

    [Fact]
    public void Spend_Insufficient_ThrowsAndLeavesState()
    {
        var wallet = WalletRules.Initial(Catalog, 0);

        var error = Assert.Throws<GameOperationException>(() => WalletRules.Spend(wallet, "COIN", 201));

        Assert.Equal(ErrorCodes.InsufficientFunds, error.Code);
        Assert.Equal(200, wallet.Currencies["COIN"]);
    }

    [Fact]
    public void TryRecordOperation_Duplicate_ReturnsFalse()
    {
        var wallet = WalletRules.Initial(Catalog, 0);

        Assert.True(WalletRules.TryRecordOperation(wallet, "op-1"));
        Assert.False(WalletRules.TryRecordOperation(wallet, "op-1"));
    }

    [Fact]
    public void TryRecordOperation_Over50_EvictsOldest()
    {
        var wallet = WalletRules.Initial(Catalog, 0);
        for (var i = 0; i <= WalletState.MaxRecentOperations; i++)
        {
            WalletRules.TryRecordOperation(wallet, $"op-{i}");
        }

        Assert.Equal(WalletState.MaxRecentOperations, wallet.RecentOperations.Count);
        Assert.DoesNotContain("op-0", wallet.RecentOperations);
        Assert.False(WalletRules.TryRecordOperation(wallet, $"op-{WalletState.MaxRecentOperations}"));
    }

    [Fact]
    public void TryRecordOperation_Empty_ThrowsConfigurationError()
    {
        var wallet = WalletRules.Initial(Catalog, 0);

        Assert.Throws<GameOperationException>(() => WalletRules.TryRecordOperation(wallet, " "));
    }

    [Fact]
    public void TryClaim_Twice_SecondReturnsFalse()
    {
        var wallet = WalletRules.Initial(Catalog, 0);

        Assert.True(WalletRules.TryClaim(wallet, "FREE_PACK"));
        Assert.False(WalletRules.TryClaim(wallet, "FREE_PACK"));
    }

    [Fact]
    public void Parse_InvalidCatalog_ThrowsConfigurationError()
    {
        var error = Assert.Throws<GameOperationException>(() =>
            WalletCatalog.Parse(JToken.Parse(@"{ ""timedEffects"": { ""X"": { ""durationSeconds"": 0 } } }")));

        Assert.Equal(ErrorCodes.ConfigurationError, error.Code);
    }
}
