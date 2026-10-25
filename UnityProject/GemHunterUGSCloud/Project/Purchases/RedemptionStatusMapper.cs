using System.Net;
using Unity.Services.CloudCode.Shared;
using GoogleStatus = Unity.Services.Economy.Model.PlayerPurchaseGoogleplaystoreResponseVerification.StatusEnum;
using AppleStatus = Unity.Services.Economy.Model.PlayerPurchaseAppleappstoreResponseVerification.StatusEnum;

namespace GemHunterUGSCloud.Purchases;

/// <summary>
/// Translates Economy verification statuses and API failures into <see cref="PurchaseRedemptionStatus"/>.
/// Unknown values are rejected: a definitive refusal never grants, and avoids endless client retries.
/// </summary>
internal static class RedemptionStatusMapper
{
    public static PurchaseRedemptionStatus FromGoogle(GoogleStatus status) => status switch
    {
        GoogleStatus.VALID => PurchaseRedemptionStatus.Delivered,
        GoogleStatus.INVALIDALREADYREDEEMED => PurchaseRedemptionStatus.AlreadyDelivered,
        // Assumption to confirm in sandbox: "valid, seen before, not yet redeemed" means nothing was granted.
        GoogleStatus.VALIDNOTREDEEMED => PurchaseRedemptionStatus.RetryLater,
        _ => PurchaseRedemptionStatus.Rejected
    };

    public static PurchaseRedemptionStatus FromApple(AppleStatus status) => status switch
    {
        AppleStatus.VALID => PurchaseRedemptionStatus.Delivered,
        AppleStatus.INVALIDALREADYREDEEMED => PurchaseRedemptionStatus.AlreadyDelivered,
        AppleStatus.VALIDNOTREDEEMED => PurchaseRedemptionStatus.RetryLater,
        _ => PurchaseRedemptionStatus.Rejected
    };

    /// <summary>
    /// Fallback when an API failure carries no readable verification status. Only errors that describe the
    /// request itself are definitive; anything else (auth, timeout, conflict, server) may be transient, and a
    /// paid purchase must stay pending rather than be dropped.
    /// </summary>
    public static PurchaseRedemptionStatus FromHttpStatus(HttpStatusCode? statusCode, ApiExceptionType type)
    {
        switch (type)
        {
            case ApiExceptionType.InvalidParameters:
                return PurchaseRedemptionStatus.Rejected;
            case ApiExceptionType.Http when statusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.NotFound
                or HttpStatusCode.UnprocessableEntity:
                return PurchaseRedemptionStatus.Rejected;
            default:
                return PurchaseRedemptionStatus.RetryLater;
        }
    }
}
