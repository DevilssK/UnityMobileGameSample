using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using GemHunterUGSCloud.Purchases;
using GemHunterUGSCloud.Services;
using NSubstitute;
using NSubstitute.Extensions;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Economy.Api;
using Unity.Services.Economy.Model;
using Xunit;
using static GemHunterUGSCloud.Tests.Support.PurchaseTestDoubles;

namespace GemHunterUGSCloud.Tests.Services;

public class FreeCoinPackTests
{
    private const string FreePackItemId = "COIN_PACK_FREE";
    private const string FreePackClaimId = "COIN_PACK_FREE_CLAIM";

    private static Task<ApiResponse<InventoryResponse>> AddInventory(IEconomyInventoryApi inventory) =>
        inventory.AddInventoryItemAsync(
            Arg.Any<IExecutionContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<AddInventoryRequest>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>());

    private static (StoreService Service, PlayerEconomyService Economy, IEconomyInventoryApi Inventory) Create(
        bool alreadyClaimed = false)
    {
        var (client, _) = Client();
        var inventory = Substitute.For<IEconomyInventoryApi>();
        client.EconomyInventory.Returns(inventory);

        // Economy refuses a second item with the same playersInventoryItemId (hypothesis 4: 409 Conflict).
        var claimed = 0;
        AddInventory(inventory).Returns(_ => Interlocked.Increment(ref claimed) == 1
            ? Task.FromResult(Response(HttpStatusCode.OK, (InventoryResponse?)null))
            : Task.FromException<ApiResponse<InventoryResponse>>(HttpFailure(HttpStatusCode.Conflict)));

        var economy = Substitute.ForPartsOf<PlayerEconomyService>(Logger<PlayerEconomyService>(), client);
        economy.Configure().GetPlayerEconomyData(Arg.Any<IExecutionContext>())
            .Returns(_ => new PlayerEconomyData { HasPurchasedFreeCoinPack = alreadyClaimed });
        economy.Configure().UpdatePlayerCurrency(Arg.Any<IExecutionContext>(), Arg.Any<string>(), Arg.Any<int>())
            .Returns(10);

        var redeemer = Substitute.For<PurchaseRedeemer>(Logger<PurchaseRedeemer>(), client);
        return (new StoreService(Logger<StoreService>(), client, economy, redeemer), economy, inventory);
    }

    [Fact]
    public async Task HandleFreeCoinPackPurchase_WhenConcurrentRequests_CreditsOnce()
    {
        var (service, economy, _) = Create();

        await Task.WhenAll(
            service.HandleFreeCoinPackPurchase(Context()),
            service.HandleFreeCoinPackPurchase(Context()));

        await economy.Received(1).UpdatePlayerCurrency(Arg.Any<IExecutionContext>(), "COIN", 10);
    }

    [Fact]
    public async Task HandleFreeCoinPackPurchase_WhenFirstClaim_LocksWithFixedIdBeforeCrediting()
    {
        var (service, economy, inventory) = Create();

        await service.HandleFreeCoinPackPurchase(Context());

        Received.InOrder(() =>
        {
            AddInventory(inventory);
            economy.UpdatePlayerCurrency(Arg.Any<IExecutionContext>(), "COIN", 10);
        });
        await inventory.Received(1).AddInventoryItemAsync(
            Arg.Any<IExecutionContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<AddInventoryRequest>(r => r.InventoryItemId == FreePackItemId && r.PlayersInventoryItemId == FreePackClaimId),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleFreeCoinPackPurchase_WhenAlreadyClaimed_DoesNotCredit()
    {
        var (service, economy, inventory) = Create(alreadyClaimed: true);

        await service.HandleFreeCoinPackPurchase(Context());

        await economy.DidNotReceive().UpdatePlayerCurrency(Arg.Any<IExecutionContext>(), Arg.Any<string>(), Arg.Any<int>());
        Assert.Empty(inventory.ReceivedCalls());
    }

    [Fact]
    public async Task TryClaimFreePackAsync_WhenUnexpectedError_Rethrows()
    {
        var (client, _) = Client();
        var inventory = Substitute.For<IEconomyInventoryApi>();
        client.EconomyInventory.Returns(inventory);
        AddInventory(inventory).Returns(
            Task.FromException<ApiResponse<InventoryResponse>>(HttpFailure(HttpStatusCode.InternalServerError)));
        var economy = new PlayerEconomyService(Logger<PlayerEconomyService>(), client);

        await Assert.ThrowsAsync<ApiException>(() => economy.TryClaimFreePackAsync(Context()));
    }
}
