using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Economy.Model;
using GoogleStatus = Unity.Services.Economy.Model.PlayerPurchaseGoogleplaystoreResponseVerification.StatusEnum;
using AppleStatus = Unity.Services.Economy.Model.PlayerPurchaseAppleappstoreResponseVerification.StatusEnum;

namespace GemHunterUGSCloud.Purchases;

/// <summary>
/// Redeems real-money purchases through Economy.
/// Invariant: Economy redeem is the single owner of the grant. This class never writes currencies or
/// inventory itself, so a valid transaction is granted at most once — Economy deduplicates per receipt
/// and reports a repeat by the same player as already redeemed, and by another player as a refusal.
/// </summary>
public class PurchaseRedeemer
{
    // Local price is optional revenue metadata the current client contract does not send.
    // Assumption to confirm in sandbox: Economy accepts an empty currency.
    private const int NoLocalCost = 0;
    private const string NoLocalCurrency = "";

    private readonly ILogger<PurchaseRedeemer> _logger;
    private readonly IGameApiClient _gameApiClient;

    public PurchaseRedeemer(ILogger<PurchaseRedeemer> logger, IGameApiClient gameApiClient)
    {
        _logger = logger;
        _gameApiClient = gameApiClient;
    }

    /// <param name="productId">Economy real-money purchase ID; Economy checks it against the receipt.</param>
    /// <param name="receiptJson">Unity IAP receipt envelope. Never logged.</param>
    public virtual async Task<PurchaseRedemptionResult> RedeemAsync(
        IExecutionContext context, string productId, string receiptJson)
    {
        // Grants go to the authenticated caller only; a call without a player (e.g. service token) is refused.
        var playerId = context.PlayerId;
        if (string.IsNullOrEmpty(playerId))
        {
            return Log(context, productId, StoreKind.Unknown,
                new PurchaseRedemptionResult(PurchaseRedemptionStatus.Rejected, "No authenticated player"));
        }

        if (string.IsNullOrEmpty(productId))
        {
            return Log(context, productId, StoreKind.Unknown,
                new PurchaseRedemptionResult(PurchaseRedemptionStatus.Rejected, "Missing product ID"));
        }

        if (!StoreReceipt.TryParse(receiptJson, out var receipt) || receipt == null)
        {
            return Log(context, productId, StoreKind.Unknown,
                new PurchaseRedemptionResult(PurchaseRedemptionStatus.Rejected, "Unreadable receipt"));
        }

        if (receipt.Store == StoreKind.Fake)
        {
            var fakeResult = FakeStorePolicy.IsAllowed(context.EnvironmentName)
                ? new PurchaseRedemptionResult(PurchaseRedemptionStatus.Simulated, "Fake store, nothing granted")
                : new PurchaseRedemptionResult(PurchaseRedemptionStatus.Rejected, "Fake store not allowed in this environment");
            return Log(context, productId, receipt.Store, fakeResult);
        }

        PurchaseRedemptionResult result;
        try
        {
            result = receipt.Store == StoreKind.GooglePlay
                ? await RedeemGooglePlayAsync(context, playerId, productId, receipt)
                : await RedeemAppleAppStoreAsync(context, playerId, productId, receipt);
        }
        catch (ApiException e)
        {
            result = FromApiException(receipt.Store, e);
        }

        return Log(context, productId, receipt.Store, result);
    }

    private async Task<PurchaseRedemptionResult> RedeemGooglePlayAsync(
        IExecutionContext context, string playerId, string productId, StoreReceipt receipt)
    {
        // The generated model validates required fields in its constructor: always pass them explicitly.
        // StoreReceipt.TryParse guarantees the store fields are present for this store.
        var request = new PlayerPurchaseGoogleplaystoreRequest(
            id: productId,
            purchaseData: receipt.GooglePurchaseData!,
            purchaseDataSignature: receipt.GoogleSignature!,
            localCost: NoLocalCost,
            localCurrency: NoLocalCurrency);
        var response = await _gameApiClient.EconomyPurchases.RedeemGooglePlayPurchaseAsync(
            context, context.AccessToken, context.ProjectId, playerId, request);
        var status = response.Data.Verification.Status;
        return new PurchaseRedemptionResult(RedemptionStatusMapper.FromGoogle(status), $"Economy: {status}");
    }

    private async Task<PurchaseRedemptionResult> RedeemAppleAppStoreAsync(
        IExecutionContext context, string playerId, string productId, StoreReceipt receipt)
    {
        var request = new PlayerPurchaseAppleappstoreRequest(
            id: productId, receipt: receipt.AppleReceipt!, localCost: NoLocalCost, localCurrency: NoLocalCurrency);
        var response = await _gameApiClient.EconomyPurchases.RedeemAppleAppStorePurchaseAsync(
            context, context.AccessToken, context.ProjectId, playerId, request);
        var status = response.Data.Verification.Status;
        return new PurchaseRedemptionResult(RedemptionStatusMapper.FromApple(status), $"Economy: {status}");
    }

    /// <summary>
    /// Economy may report a verification failure as an error response carrying the verification status.
    /// Use that status when present; otherwise fall back on the transport/HTTP failure kind.
    /// </summary>
    private static PurchaseRedemptionResult FromApiException(StoreKind store, ApiException exception)
    {
        var statusName = TryReadVerificationStatus(exception.Response?.RawContent);
        if (statusName != null)
        {
            var normalized = statusName.Replace("_", string.Empty);
            if (store == StoreKind.GooglePlay
                && Enum.TryParse<GoogleStatus>(normalized, true, out var googleStatus))
            {
                return new PurchaseRedemptionResult(RedemptionStatusMapper.FromGoogle(googleStatus), $"Economy: {statusName}");
            }

            if (store == StoreKind.AppleAppStore
                && Enum.TryParse<AppleStatus>(normalized, true, out var appleStatus))
            {
                return new PurchaseRedemptionResult(RedemptionStatusMapper.FromApple(appleStatus), $"Economy: {statusName}");
            }
        }

        var statusCode = exception.Response?.StatusCode;
        var mapped = RedemptionStatusMapper.FromHttpStatus(
            exception.Type == ApiExceptionType.Http ? statusCode : null, exception.Type);
        return new PurchaseRedemptionResult(mapped, $"Economy error: {exception.Type} {(int?)statusCode}");
    }

    private static string? TryReadVerificationStatus(string? rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return null;
        }

        try
        {
            // Recursive descent: the status may sit at the root or inside an error envelope.
            return JToken.Parse(rawContent).SelectToken("$..verification.status")?.Value<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private PurchaseRedemptionResult Log(
        IExecutionContext context, string productId, StoreKind store, PurchaseRedemptionResult result)
    {
        var level = result.Status is PurchaseRedemptionStatus.Delivered or PurchaseRedemptionStatus.AlreadyDelivered
            ? LogLevel.Information
            : LogLevel.Warning;
        _logger.Log(level,
            "Purchase redemption {Status} for product {ProductId} on {Store} (correlation {CorrelationId}): {Reason}",
            result.Status, productId, store, context.CorrelationId, result.Reason);
        return result;
    }
}
