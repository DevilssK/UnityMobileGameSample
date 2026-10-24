using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevilCorp.MobileFoundation.Purchases.Tests
{
    /// <summary>
    /// Stands in for Unity's main-thread context: posted continuations run only when the test pumps them.
    /// </summary>
    internal sealed class QueueSynchronizationContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue =
            new BlockingCollection<(SendOrPostCallback, object?)>();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public bool PumpOne(int timeoutMs)
        {
            if (!_queue.TryTake(out var item, timeoutMs))
            {
                return false;
            }

            item.Callback(item.State);
            return true;
        }
    }

    public class MainThreadTests
    {
        [Fact]
        public void Fulfillment_WhenBackendAnswersOnAnotherThread_ConfirmsAndReportsOnCallerContext()
        {
            var previous = SynchronizationContext.Current;
            var mainThread = new QueueSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(mainThread);
            try
            {
                var store = new ContextRecordingStore();
                var fulfillment = new FakeFulfillment();
                var coordinator = new PurchaseCoordinator(store, fulfillment);
                SynchronizationContext? reportContext = null;
                coordinator.PurchaseCompleted += _ => reportContext = SynchronizationContext.Current;

                store.RaisePending(new StorePurchase("COIN_PACK_100", "GPA.1", "receipt"));
                Task.Run(() => fulfillment.Pending[0].SetResult(FulfillmentStatus.Delivered)).Wait();
                while (store.ConfirmContext == null && mainThread.PumpOne(2000))
                {
                }

                Assert.Same(mainThread, store.ConfirmContext);
                Assert.Same(mainThread, reportContext);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        private sealed class ContextRecordingStore : IStoreGateway
        {
            public SynchronizationContext? ConfirmContext { get; private set; }
            public bool IsReady => true;
            public event System.Action<StorePurchase>? PurchasePending;
            public event System.Action<string, StoreFailure>? PurchaseFailed { add { } remove { } }
            public event System.Action<string>? PurchaseDeferred { add { } remove { } }
            public void Purchase(string productId) { }
            public void Confirm(string transactionId) => ConfirmContext = SynchronizationContext.Current;
            public void RaisePending(StorePurchase purchase) => PurchasePending?.Invoke(purchase);
        }
    }
}
