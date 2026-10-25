using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DevilCorp.MobileFoundation.Purchases.Tests
{
    internal sealed class FakeStoreGateway : IStoreGateway
    {
        public bool IsReady { get; set; } = true;
        public List<string> Purchases { get; } = new List<string>();
        public List<string> Confirmations { get; } = new List<string>();
        public int ReconnectRequests { get; private set; }

        public event Action<StorePurchase>? PurchasePending;
        public event Action<string, StoreFailure>? PurchaseFailed;
        public event Action<string>? PurchaseDeferred;

        public void Purchase(string productId) => Purchases.Add(productId);
        public void Confirm(string transactionId) => Confirmations.Add(transactionId);
        public void Reconnect() => ReconnectRequests++;

        public void RaisePending(StorePurchase purchase) => PurchasePending?.Invoke(purchase);
        public void RaiseFailed(string productId, StoreFailure failure) => PurchaseFailed?.Invoke(productId, failure);
        public void RaiseDeferred(string productId) => PurchaseDeferred?.Invoke(productId);
    }

    /// <summary>
    /// Backend double: each call waits on its own completion source so tests decide when and how it ends.
    /// </summary>
    internal sealed class FakeFulfillment : IPurchaseFulfillment
    {
        private readonly Queue<Func<Task<FulfillmentStatus>>> _scripted = new Queue<Func<Task<FulfillmentStatus>>>();

        public List<StorePurchase> Calls { get; } = new List<StorePurchase>();
        public List<TaskCompletionSource<FulfillmentStatus>> Pending { get; } = new List<TaskCompletionSource<FulfillmentStatus>>();

        public void Returns(FulfillmentStatus status) => _scripted.Enqueue(() => Task.FromResult(status));
        public void Throws(Exception exception) => _scripted.Enqueue(() => Task.FromException<FulfillmentStatus>(exception));

        public Task<FulfillmentStatus> FulfillAsync(StorePurchase purchase, CancellationToken cancellationToken)
        {
            Calls.Add(purchase);
            if (_scripted.Count > 0)
            {
                return _scripted.Dequeue()();
            }

            var completion = new TaskCompletionSource<FulfillmentStatus>();
            Pending.Add(completion);
            return completion.Task;
        }
    }
}
