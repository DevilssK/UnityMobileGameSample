namespace GemHunterUGSCloud.Purchases;

/// <param name="Reason">Short diagnostic without any store proof (receipt, payload, signature).</param>
public sealed record PurchaseRedemptionResult(PurchaseRedemptionStatus Status, string Reason);
