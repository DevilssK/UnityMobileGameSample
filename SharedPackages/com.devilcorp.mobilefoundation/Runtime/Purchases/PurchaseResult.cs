#nullable enable

namespace DevilCorp.MobileFoundation.Purchases
{
    public sealed class PurchaseResult
    {
        public PurchaseResult(string productId, PurchaseOutcome outcome, string message = "")
        {
            ProductId = productId;
            Outcome = outcome;
            Message = message;
        }

        public string ProductId { get; }
        public PurchaseOutcome Outcome { get; }

        /// <summary>Short diagnostic for logs; never contains store proofs.</summary>
        public string Message { get; }

        public override string ToString() => $"{ProductId}: {Outcome} {Message}".TrimEnd();
    }
}
