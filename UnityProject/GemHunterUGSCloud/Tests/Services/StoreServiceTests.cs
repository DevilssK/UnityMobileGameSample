using System;
using System.Threading.Tasks;
using GemHunterUGSCloud.Models;
using GemHunterUGSCloud.Purchases;
using GemHunterUGSCloud.Services;
using NSubstitute;
using NSubstitute.Extensions;
using Unity.Services.CloudCode.Core;
using Xunit;
using static GemHunterUGSCloud.Tests.Support.PurchaseTestDoubles;

namespace GemHunterUGSCloud.Tests.Services;

public class StoreServiceTests
{
    private const string ProductId = "COIN_PACK_100";

    private static (StoreService Service, PlayerEconomyService Economy) Create(PurchaseRedemptionStatus status)
    {
        var (client, _) = Client();
        var economy = Substitute.ForPartsOf<PlayerEconomyService>(Logger<PlayerEconomyService>(), client);
        economy.Configure().GetPlayerEconomyData(Arg.Any<IExecutionContext>())
            .Returns(new PlayerEconomyData());
        var redeemer = Substitute.For<PurchaseRedeemer>(Logger<PurchaseRedeemer>(), client);
        redeemer.RedeemAsync(default!, default!, default!)
            .ReturnsForAnyArgs(new PurchaseRedemptionResult(status, "test"));
        return (new StoreService(Logger<StoreService>(), client, economy, redeemer), economy);
    }

    [Fact]
    public async Task HandlePurchase_WhenRejected_ThrowsWithRejectedCodeAndDoesNotReadEconomy()
    {
        var (service, economy) = Create(PurchaseRedemptionStatus.Rejected);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.HandlePurchase(Context(), ProductId, GoogleReceipt(), "GPA.1"));

        Assert.StartsWith("PURCHASE_REJECTED", error.Message);
        await economy.DidNotReceive().GetPlayerEconomyData(Arg.Any<IExecutionContext>());
    }

    [Fact]
    public async Task HandlePurchase_WhenRetryLater_ThrowsWithRetryCode()
    {
        var (service, _) = Create(PurchaseRedemptionStatus.RetryLater);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.HandlePurchase(Context(), ProductId, GoogleReceipt(), "GPA.1"));

        Assert.StartsWith("PURCHASE_RETRY_LATER", error.Message);
    }

    [Theory]
    [InlineData(PurchaseRedemptionStatus.Delivered)]
    [InlineData(PurchaseRedemptionStatus.AlreadyDelivered)]
    [InlineData(PurchaseRedemptionStatus.Simulated)]
    public async Task HandlePurchase_WhenDeliveredAlreadyDeliveredOrSimulated_ReturnsEconomyData(PurchaseRedemptionStatus status)
    {
        var (service, economy) = Create(status);

        var data = await service.HandlePurchase(Context(), ProductId, GoogleReceipt(), "GPA.1");

        Assert.NotNull(data);
        await economy.Received(1).GetPlayerEconomyData(Arg.Any<IExecutionContext>());
    }
}
