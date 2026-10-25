namespace DevilCorp.MobileFoundation.Purchases
{
    /// <summary>
    /// Why the store ended a purchase without a paid transaction.
    /// </summary>
    public enum StoreFailure
    {
        Cancelled,
        Failed,
        Unavailable
    }
}
