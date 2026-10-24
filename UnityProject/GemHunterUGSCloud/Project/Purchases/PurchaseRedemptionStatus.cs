namespace GemHunterUGSCloud.Purchases;

/// <summary>
/// Outcome of a purchase redemption, as seen by the game.
/// </summary>
public enum PurchaseRedemptionStatus
{
    /// <summary>Verified and granted by this request.</summary>
    Delivered,

    /// <summary>Already granted to this player by an earlier request; nothing granted again.</summary>
    AlreadyDelivered,

    /// <summary>Fake store accepted in a development environment; nothing granted.</summary>
    Simulated,

    /// <summary>Definitive refusal (invalid proof, other player, wrong product or environment).</summary>
    Rejected,

    /// <summary>Transient failure; the client must keep the purchase pending and retry.</summary>
    RetryLater
}
