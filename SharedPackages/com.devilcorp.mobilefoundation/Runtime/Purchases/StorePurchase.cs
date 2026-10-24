#nullable enable

namespace DevilCorp.MobileFoundation.Purchases
{
    /// <summary>
    /// A paid transaction reported by the store and waiting for backend validation.
    /// </summary>
    public sealed class StorePurchase
    {
        public StorePurchase(string productId, string transactionId, string receipt)
        {
            ProductId = productId;
            TransactionId = transactionId;
            Receipt = receipt;
        }

        public string ProductId { get; }

        /// <summary>Store transaction identifier; safe to log for support correlation.</summary>
        public string TransactionId { get; }

        /// <summary>Store proof sent to the backend. Never log it.</summary>
        public string Receipt { get; }

        public override string ToString() => $"{ProductId} ({TransactionId})";
    }
}
