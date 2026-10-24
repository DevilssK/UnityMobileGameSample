namespace DevilCorp.MobileFoundation.Purchases
{
    /// <summary>
    /// Terminal outcome of a purchase as seen by the game.
    /// </summary>
    public enum PurchaseOutcome
    {
        /// <summary>Validated by the backend, granted and confirmed with the store.</summary>
        Delivered,

        /// <summary>Cancelled by the player, or the caller stopped waiting.</summary>
        Cancelled,

        /// <summary>The store reported a failure; nothing was charged or granted.</summary>
        Failed,

        /// <summary>Awaiting approval (Ask-to-Buy, deferred payment); nothing granted yet.</summary>
        Deferred,

        /// <summary>Definitively refused by the backend; the transaction is not confirmed.</summary>
        Rejected,

        /// <summary>Paid but not validated yet (offline, backend unavailable); kept pending and retried later.</summary>
        RetryLater,

        /// <summary>The store is not ready or the product is unknown.</summary>
        Unavailable,

        /// <summary>Another purchase is still running.</summary>
        AlreadyInProgress
    }
}
