#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevilCorp.MobileFoundation.Purchases
{
    /// <summary>
    /// Drives a purchase from the store dialog to backend validation and store confirmation.
    /// Invariants:
    /// - A transaction is confirmed with the store only after the backend returned Delivered.
    /// - A transaction is sent to the backend at most once at a time, whatever the store re-delivers.
    /// - The "purchase in progress" state is released on every terminal outcome, including exceptions.
    /// - Cancelling the caller's wait never abandons a paid transaction.
    /// - Continuations resume on the caller's synchronization context (Unity main thread): the store SDK
    ///   and UI listeners are only touched from there.
    /// </summary>
    public sealed class PurchaseCoordinator : IPurchaseService, IDisposable
    {
        private readonly IStoreGateway _store;
        private readonly IPurchaseFulfillment _fulfillment;
        private readonly Action<string> _log;
        private readonly object _gate = new object();
        private readonly HashSet<string> _inFlight = new HashSet<string>();
        private readonly Dictionary<string, StorePurchase> _awaitingRetry = new Dictionary<string, StorePurchase>();

        private string? _activeProductId;
        // Transaction the store produced for the player's active purchase; null until it is paid.
        private string? _activeTransactionId;
        private TaskCompletionSource<PurchaseResult>? _activeCompletion;

        public PurchaseCoordinator(IStoreGateway store, IPurchaseFulfillment fulfillment, Action<string>? log = null)
        {
            _store = store;
            _fulfillment = fulfillment;
            _log = log ?? (_ => { });

            _store.PurchasePending += HandlePurchasePending;
            _store.PurchaseFailed += HandlePurchaseFailed;
            _store.PurchaseDeferred += HandlePurchaseDeferred;
        }

        public event Action<PurchaseResult>? PurchaseCompleted;

        public bool IsReady => _store.IsReady;

        public bool IsPurchaseInProgress
        {
            get { lock (_gate) { return _activeProductId != null; } }
        }

        /// <summary>Paid transactions waiting for a successful backend validation.</summary>
        public int PendingFulfillmentCount
        {
            get { lock (_gate) { return _awaitingRetry.Count; } }
        }

        public async Task<PurchaseResult> PurchaseAsync(string productId, CancellationToken cancellationToken)
        {
            if (!_store.IsReady)
            {
                // A failed connection or product fetch is not retried by the SDK: ask again on demand.
                _store.Reconnect();
                return Report(new PurchaseResult(productId, PurchaseOutcome.Unavailable, "Store not ready"));
            }

            TaskCompletionSource<PurchaseResult> completion;
            lock (_gate)
            {
                if (_activeProductId != null)
                {
                    return new PurchaseResult(productId, PurchaseOutcome.AlreadyInProgress, $"{_activeProductId} in progress");
                }

                completion = new TaskCompletionSource<PurchaseResult>(TaskCreationOptions.RunContinuationsAsynchronously);
                _activeProductId = productId;
                _activeTransactionId = null;
                _activeCompletion = completion;
            }

            using (cancellationToken.Register(() => ReleaseActive(completion,
                       new PurchaseResult(productId, PurchaseOutcome.Cancelled, "Caller stopped waiting"))))
            {
                _store.Purchase(productId);
                return await completion.Task;
            }
        }

        public async Task RetryPendingFulfillmentsAsync(CancellationToken cancellationToken)
        {
            List<StorePurchase> retries;
            lock (_gate)
            {
                retries = new List<StorePurchase>(_awaitingRetry.Values);
            }

            foreach (var purchase in retries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await FulfillAsync(purchase);
            }
        }

        public void Dispose()
        {
            _store.PurchasePending -= HandlePurchasePending;
            _store.PurchaseFailed -= HandlePurchaseFailed;
            _store.PurchaseDeferred -= HandlePurchaseDeferred;
        }

        private void HandlePurchasePending(StorePurchase purchase)
        {
            lock (_gate)
            {
                // Bind the player's purchase to the first new transaction of that product. Recovered
                // transactions (already known) must not answer the player's current purchase.
                if (_activeProductId == purchase.ProductId && _activeTransactionId == null
                    && !_inFlight.Contains(purchase.TransactionId)
                    && !_awaitingRetry.ContainsKey(purchase.TransactionId))
                {
                    _activeTransactionId = purchase.TransactionId;
                }
            }

            // Fire and forget: FulfillAsync never throws, and the result is reported through events.
            _ = FulfillAsync(purchase);
        }

        private void HandlePurchaseFailed(string productId, StoreFailure failure)
        {
            var outcome = failure switch
            {
                StoreFailure.Cancelled => PurchaseOutcome.Cancelled,
                StoreFailure.Unavailable => PurchaseOutcome.Unavailable,
                _ => PurchaseOutcome.Failed
            };
            CompleteFromStore(new PurchaseResult(productId, outcome, $"Store: {failure}"));
        }

        private void HandlePurchaseDeferred(string productId)
        {
            CompleteFromStore(new PurchaseResult(productId, PurchaseOutcome.Deferred, "Awaiting approval"));
        }

        private async Task FulfillAsync(StorePurchase purchase)
        {
            lock (_gate)
            {
                if (!_inFlight.Add(purchase.TransactionId))
                {
                    return;
                }
            }

            FulfillmentStatus status;
            try
            {
                status = await _fulfillment.FulfillAsync(purchase, CancellationToken.None);
            }
            catch (Exception e)
            {
                _log($"Fulfillment of {purchase} failed: {e.GetType().Name}");
                status = FulfillmentStatus.RetryLater;
            }

            lock (_gate)
            {
                _inFlight.Remove(purchase.TransactionId);
                if (status == FulfillmentStatus.RetryLater)
                {
                    _awaitingRetry[purchase.TransactionId] = purchase;
                }
                else
                {
                    _awaitingRetry.Remove(purchase.TransactionId);
                }
            }

            if (status == FulfillmentStatus.Delivered)
            {
                _store.Confirm(purchase.TransactionId);
            }

            var outcome = status switch
            {
                FulfillmentStatus.Delivered => PurchaseOutcome.Delivered,
                FulfillmentStatus.Rejected => PurchaseOutcome.Rejected,
                _ => PurchaseOutcome.RetryLater
            };
            CompleteTransaction(purchase.TransactionId,
                new PurchaseResult(purchase.ProductId, outcome, $"Transaction {purchase.TransactionId}"));
        }

        /// <summary>
        /// A store failure or deferral carries no transaction: it ends the purchase the store dialog was opened
        /// for, whatever product ID the SDK reports (it may report an unknown product).
        /// </summary>
        private void CompleteFromStore(PurchaseResult result)
        {
            TaskCompletionSource<PurchaseResult>? completion = null;
            lock (_gate)
            {
                if (_activeProductId != null && _activeTransactionId == null)
                {
                    completion = _activeCompletion;
                }
            }

            Finish(completion, result);
        }

        private void CompleteTransaction(string transactionId, PurchaseResult result)
        {
            TaskCompletionSource<PurchaseResult>? completion = null;
            lock (_gate)
            {
                if (_activeTransactionId == transactionId)
                {
                    completion = _activeCompletion;
                }
            }

            Finish(completion, result);
        }

        private void Finish(TaskCompletionSource<PurchaseResult>? completion, PurchaseResult result)
        {

            if (completion != null)
            {
                ReleaseActive(completion, result);
            }

            Report(result);
        }

        private void ReleaseActive(TaskCompletionSource<PurchaseResult> completion, PurchaseResult result)
        {
            lock (_gate)
            {
                if (_activeCompletion == completion)
                {
                    _activeProductId = null;
                    _activeTransactionId = null;
                    _activeCompletion = null;
                }
            }

            completion.TrySetResult(result);
        }

        private PurchaseResult Report(PurchaseResult result)
        {
            _log($"Purchase {result}");
            PurchaseCompleted?.Invoke(result);
            return result;
        }
    }
}
