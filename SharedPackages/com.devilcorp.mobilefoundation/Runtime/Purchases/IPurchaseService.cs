#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace DevilCorp.MobileFoundation.Purchases
{
    /// <summary>
    /// What the game uses to sell products. It never calls the store SDK or the backend directly.
    /// </summary>
    public interface IPurchaseService
    {
        bool IsReady { get; }
        bool IsPurchaseInProgress { get; }

        /// <summary>
        /// Raised for every purchase result, including transactions recovered without player action
        /// (previous session, retried validation).
        /// </summary>
        event Action<PurchaseResult>? PurchaseCompleted;

        /// <summary>
        /// Buys a product and waits for its outcome. Cancelling only stops waiting: a paid transaction
        /// keeps being validated and confirmed, and is reported through <see cref="PurchaseCompleted"/>.
        /// </summary>
        Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken);

        /// <summary>
        /// Re-validates paid transactions left pending by a transient failure. Call it on explicit
        /// occasions (startup, sign-in, opening the store): there is no automatic retry loop.
        /// </summary>
        Task RetryPendingFulfillmentsAsync(CancellationToken cancellationToken);
    }
}
