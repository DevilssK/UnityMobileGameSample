namespace DevilCorp.MobileFoundation.Server.State;

/// <summary>Prefixes of Cloud Code error messages; clients branch on them.</summary>
public static class ErrorCodes
{
    public const string PurchaseRejected = "PURCHASE_REJECTED";
    public const string PurchaseRetryLater = "PURCHASE_RETRY_LATER";
    public const string InsufficientFunds = "INSUFFICIENT_FUNDS";
    public const string RetryLater = "RETRY_LATER";
    public const string ConfigurationError = "CONFIGURATION_ERROR";
}
