using System;
using System.Net;
using System.Threading.Tasks;
using GemHunterUGSCloud.Purchases;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Unity.Services.CloudCode.Core;
using Unity.Services.Economy.Api;
using Unity.Services.Economy.Model;
using Xunit;
using static GemHunterUGSCloud.Tests.Support.PurchaseTestDoubles;
using GoogleStatus = Unity.Services.Economy.Model.PlayerPurchaseGoogleplaystoreResponseVerification.StatusEnum;
using AppleStatus = Unity.Services.Economy.Model.PlayerPurchaseAppleappstoreResponseVerification.StatusEnum;

namespace GemHunterUGSCloud.Tests.Purchases;

public class PurchaseRedeemerTests
{
    private const string ProductId = "COIN_PACK_100";

    private static void SetupGoogle(IEconomyPurchasesApi purchases, params GoogleStatus[] statuses)
    {
        var responses = Array.ConvertAll(statuses, GoogleResponse);
        purchases.RedeemGooglePlayPurchaseAsync(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(responses[0], responses[1..]);
    }

    private static void ThrowGoogle(IEconomyPurchasesApi purchases, Exception exception)
    {
        purchases.RedeemGooglePlayPurchaseAsync(default!, default!, default!, default!, default!)
            .ThrowsAsyncForAnyArgs(exception);
    }

    private static PurchaseRedeemer Redeemer(Unity.Services.CloudCode.Apis.IGameApiClient client) =>
        new(Logger<PurchaseRedeemer>(), client);

    private static void AssertNoGrantApiTouched(Unity.Services.CloudCode.Apis.IGameApiClient client)
    {
        _ = client.DidNotReceive().EconomyCurrencies;
        _ = client.DidNotReceive().EconomyInventory;
    }

    [Fact]
    public async Task RedeemAsync_WhenGoogleReceiptValid_SendsExtractedPayloadAndReturnsDelivered()
    {
        var (client, purchases) = Client();
        SetupGoogle(purchases, GoogleStatus.VALID);

        var result = await Redeemer(client).RedeemAsync(Context(), ProductId, GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Delivered, result.Status);
        await purchases.Received(1).RedeemGooglePlayPurchaseAsync(
            Arg.Any<IExecutionContext>(), "token", "project-1", "player-1",
            Arg.Is<PlayerPurchaseGoogleplaystoreRequest>(r =>
                r.Id == ProductId && r.PurchaseData == GooglePurchaseData && r.PurchaseDataSignature == GoogleSignature),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), AnyToken);
    }

    [Fact]
    public async Task RedeemAsync_WhenAppleReceiptValid_SendsPayloadAndReturnsDelivered()
    {
        var (client, purchases) = Client();
        purchases.RedeemAppleAppStorePurchaseAsync(default!, default!, default!, default!, default!)
            .ReturnsForAnyArgs(AppleResponse(AppleStatus.VALID));

        var result = await Redeemer(client).RedeemAsync(Context(), ProductId, AppleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Delivered, result.Status);
        await purchases.Received(1).RedeemAppleAppStorePurchaseAsync(
            Arg.Any<IExecutionContext>(), "token", "project-1", "player-1",
            Arg.Is<PlayerPurchaseAppleappstoreRequest>(r => r.Id == ProductId && r.Receipt == "MIIT..."),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), AnyToken);
    }

    [Fact]
    public async Task RedeemAsync_WhenValid_NeverWritesCurrenciesOrInventory()
    {
        var (client, purchases) = Client();
        SetupGoogle(purchases, GoogleStatus.VALID);

        await Redeemer(client).RedeemAsync(Context(), ProductId, GoogleReceipt());

        AssertNoGrantApiTouched(client);
    }

    [Fact]
    public async Task RedeemAsync_WhenCalledTwiceBySamePlayer_ReturnsAlreadyDeliveredWithoutSecondGrant()
    {
        var (client, purchases) = Client();
        SetupGoogle(purchases, GoogleStatus.VALID, GoogleStatus.INVALIDALREADYREDEEMED);
        var redeemer = Redeemer(client);

        var first = await redeemer.RedeemAsync(Context(), ProductId, GoogleReceipt());
        var second = await redeemer.RedeemAsync(Context(), ProductId, GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Delivered, first.Status);
        Assert.Equal(PurchaseRedemptionStatus.AlreadyDelivered, second.Status);
        AssertNoGrantApiTouched(client);
    }

    [Fact]
    public async Task RedeemAsync_WhenAlreadyRedeemedArrivesAsException_ReturnsAlreadyDelivered()
    {
        var (client, purchases) = Client();
        ThrowGoogle(purchases, HttpFailure((HttpStatusCode)422,
            "{\"verification\":{\"status\":\"INVALID_ALREADY_REDEEMED\"}}"));

        var result = await Redeemer(client).RedeemAsync(Context(), ProductId, GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.AlreadyDelivered, result.Status);
    }

    [Fact]
    public async Task RedeemAsync_WhenOtherPlayerArrivesAsException_IsRejected()
    {
        var (client, purchases) = Client();
        ThrowGoogle(purchases, HttpFailure((HttpStatusCode)422,
            "{\"verification\":{\"status\":\"INVALID_ANOTHER_PLAYER\"}}"));

        var result = await Redeemer(client).RedeemAsync(Context(), ProductId, GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task RedeemAsync_WhenReceiptBelongsToOtherPlayer_IsRejected()
    {
        var (client, purchases) = Client();
        SetupGoogle(purchases, GoogleStatus.INVALIDANOTHERPLAYER);

        var result = await Redeemer(client).RedeemAsync(Context(), ProductId, GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task RedeemAsync_WhenConcurrentIdenticalRequests_ProducesOneDeliveredAndOneAlreadyDelivered()
    {
        var (client, purchases) = Client();
        SetupGoogle(purchases, GoogleStatus.VALID, GoogleStatus.INVALIDALREADYREDEEMED);
        var redeemer = Redeemer(client);

        var results = await Task.WhenAll(
            redeemer.RedeemAsync(Context(), ProductId, GoogleReceipt()),
            redeemer.RedeemAsync(Context(), ProductId, GoogleReceipt()));

        Assert.Single(results, r => r.Status == PurchaseRedemptionStatus.Delivered);
        Assert.Single(results, r => r.Status == PurchaseRedemptionStatus.AlreadyDelivered);
        AssertNoGrantApiTouched(client);
    }

    [Fact]
    public async Task RedeemAsync_WhenStoreIsFakeInProduction_IsRejectedWithoutCallingEconomy()
    {
        var (client, purchases) = Client();

        var result = await Redeemer(client).RedeemAsync(Context("production"), ProductId, FakeReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Rejected, result.Status);
        Assert.Empty(purchases.ReceivedCalls());
        AssertNoGrantApiTouched(client);
    }

    [Fact]
    public async Task RedeemAsync_WhenStoreIsFakeInDevelopment_ReturnsSimulatedWithoutCallingEconomy()
    {
        var (client, purchases) = Client();

        var result = await Redeemer(client).RedeemAsync(Context("development"), ProductId, FakeReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Simulated, result.Status);
        Assert.Empty(purchases.ReceivedCalls());
        AssertNoGrantApiTouched(client);
    }

    [Fact]
    public async Task RedeemAsync_WhenGooglePayloadMalformed_IsRejectedWithoutCallingEconomy()
    {
        var (client, purchases) = Client();

        var result = await Redeemer(client).RedeemAsync(Context(), ProductId, Envelope("GooglePlay", "not json"));

        Assert.Equal(PurchaseRedemptionStatus.Rejected, result.Status);
        Assert.Empty(purchases.ReceivedCalls());
    }

    [Fact]
    public async Task RedeemAsync_WhenEconomyNetworkFailure_ReturnsRetryLater()
    {
        var (client, purchases) = Client();
        ThrowGoogle(purchases, NetworkFailure());

        var result = await Redeemer(client).RedeemAsync(Context(), ProductId, GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.RetryLater, result.Status);
    }

    [Fact]
    public async Task RedeemAsync_WhenEconomyServerErrorWithoutStatus_ReturnsRetryLater()
    {
        var (client, purchases) = Client();
        ThrowGoogle(purchases, HttpFailure(HttpStatusCode.ServiceUnavailable, "<html>oops</html>"));

        var result = await Redeemer(client).RedeemAsync(Context(), ProductId, GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.RetryLater, result.Status);
    }

    [Fact]
    public async Task RedeemAsync_WhenProductIdMismatch_IsRejected()
    {
        var (client, purchases) = Client();
        SetupGoogle(purchases, GoogleStatus.INVALIDPRODUCTIDMISMATCH);

        var result = await Redeemer(client).RedeemAsync(Context(), ProductId, GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task RedeemAsync_WhenUnknownProductRejectedByEconomy_IsRejected()
    {
        var (client, purchases) = Client();
        ThrowGoogle(purchases, HttpFailure(HttpStatusCode.NotFound, "{\"title\":\"Not Found\"}"));

        var result = await Redeemer(client).RedeemAsync(Context(), "UNKNOWN", GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Rejected, result.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task RedeemAsync_WhenProductIdMissing_IsRejectedWithoutCallingEconomy(string? productId)
    {
        var (client, purchases) = Client();

        var result = await Redeemer(client).RedeemAsync(Context(), productId!, GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Rejected, result.Status);
        Assert.Empty(purchases.ReceivedCalls());
    }

    [Fact]
    public async Task RedeemAsync_WhenNoAuthenticatedPlayer_IsRejectedWithoutCallingEconomy()
    {
        var (client, purchases) = Client();
        var context = Context();
        context.PlayerId.Returns((string?)null);

        var result = await Redeemer(client).RedeemAsync(context, ProductId, GoogleReceipt());

        Assert.Equal(PurchaseRedemptionStatus.Rejected, result.Status);
        Assert.Empty(purchases.ReceivedCalls());
    }

    [Fact]
    public async Task RedeemAsync_WhenUnexpectedExceptionOccurs_Propagates()
    {
        var (client, purchases) = Client();
        ThrowGoogle(purchases, new InvalidOperationException("bug"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Redeemer(client).RedeemAsync(Context(), ProductId, GoogleReceipt()));
    }
}
