namespace DevilCorp.MobileFoundation.Purchases
{
    /// <summary>
    /// Backend verdict on a paid transaction.
    /// </summary>
    public enum FulfillmentStatus
    {
        /// <summary>Granted now, or already granted to this player earlier: the transaction can be confirmed.</summary>
        Delivered,

        /// <summary>Definitive refusal: never confirm, never retry automatically.</summary>
        Rejected,

        /// <summary>Not decided (offline, backend unavailable): keep the transaction pending.</summary>
        RetryLater
    }
}
