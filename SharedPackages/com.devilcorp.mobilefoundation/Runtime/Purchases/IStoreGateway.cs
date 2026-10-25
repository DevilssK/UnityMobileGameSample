#nullable enable

using System;

namespace DevilCorp.MobileFoundation.Purchases
{
    /// <summary>
    /// Boundary over the platform store SDK. Implementations only translate the SDK; they never grant anything.
    /// </summary>
    public interface IStoreGateway
    {
        bool IsReady { get; }

        /// <summary>A paid transaction to validate: a new purchase, or one left unconfirmed by a previous session.</summary>
        event Action<StorePurchase>? PurchasePending;

        /// <summary>Product ID and cause of a purchase that ended without payment.</summary>
        event Action<string, StoreFailure>? PurchaseFailed;

        /// <summary>Product ID of a purchase awaiting approval; nothing may be granted yet.</summary>
        event Action<string>? PurchaseDeferred;

        void Purchase(string productId);

        /// <summary>Finalizes a transaction with the store. Call only after the backend delivered it.</summary>
        void Confirm(string transactionId);

        /// <summary>Retries a failed store connection or product fetch; no-op when ready or already connecting.</summary>
        void Reconnect();
    }
}
