using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DevilCorp.MobileFoundation.Purchases.Tests
{
    public class PurchaseCoordinatorTests
    {
        private const string ProductId = "COIN_PACK_100";
        private const string OtherProductId = "MEGA_PACK";

        private readonly FakeStoreGateway _store = new FakeStoreGateway();
        private readonly FakeFulfillment _fulfillment = new FakeFulfillment();
        private readonly List<PurchaseResult> _completed = new List<PurchaseResult>();

        private PurchaseCoordinator CreateCoordinator()
        {
            var coordinator = new PurchaseCoordinator(_store, _fulfillment);
            coordinator.PurchaseCompleted += _completed.Add;
            return coordinator;
        }

        private static StorePurchase Paid(string transactionId = "GPA.1", string productId = ProductId) =>
            new StorePurchase(productId, transactionId, "{\"Store\":\"GooglePlay\",\"Payload\":\"secret-receipt\"}");

        [Fact]
        public async Task PurchaseAsync_WhenStoreNotReady_ReturnsUnavailable()
        {
            _store.IsReady = false;
            var coordinator = CreateCoordinator();

            var result = await coordinator.PurchaseAsync(ProductId, CancellationToken.None);

            Assert.Equal(PurchaseOutcome.Unavailable, result.Outcome);
            Assert.Empty(_store.Purchases);
        }

        [Fact]
        public async Task PurchaseAsync_WhenAnotherPurchaseInProgress_ReturnsAlreadyInProgress()
        {
            var coordinator = CreateCoordinator();
            var first = coordinator.PurchaseAsync(ProductId, CancellationToken.None);

            var second = await coordinator.PurchaseAsync(OtherProductId, CancellationToken.None);

            Assert.Equal(PurchaseOutcome.AlreadyInProgress, second.Outcome);
            Assert.False(first.IsCompleted);
            Assert.Equal(new[] { ProductId }, _store.Purchases);
        }

        [Fact]
        public async Task PurchaseAsync_WhenDelivered_ConfirmsOnceAndReturnsDelivered()
        {
            _fulfillment.Returns(FulfillmentStatus.Delivered);
            var coordinator = CreateCoordinator();
            var purchase = coordinator.PurchaseAsync(ProductId, CancellationToken.None);

            _store.RaisePending(Paid());
            var result = await purchase;

            Assert.Equal(PurchaseOutcome.Delivered, result.Outcome);
            Assert.Equal(new[] { "GPA.1" }, _store.Confirmations);
            Assert.False(coordinator.IsPurchaseInProgress);
        }

        [Fact]
        public async Task PurchaseAsync_WhenStoreCancels_ReturnsCancelledAndAllowsNewPurchase()
        {
            var coordinator = CreateCoordinator();
            var purchase = coordinator.PurchaseAsync(ProductId, CancellationToken.None);

            _store.RaiseFailed(ProductId, StoreFailure.Cancelled);

            Assert.Equal(PurchaseOutcome.Cancelled, (await purchase).Outcome);
            Assert.False(coordinator.IsPurchaseInProgress);
            Assert.Empty(_fulfillment.Calls);
        }

        [Fact]
        public async Task Purchase_AfterFailure_AllowsNewPurchase()
        {
            var coordinator = CreateCoordinator();
            var failed = coordinator.PurchaseAsync(ProductId, CancellationToken.None);
            _store.RaiseFailed(ProductId, StoreFailure.Failed);
            Assert.Equal(PurchaseOutcome.Failed, (await failed).Outcome);

            _fulfillment.Returns(FulfillmentStatus.Delivered);
            var retry = coordinator.PurchaseAsync(ProductId, CancellationToken.None);
            _store.RaisePending(Paid());

            Assert.Equal(PurchaseOutcome.Delivered, (await retry).Outcome);
            Assert.Equal(new[] { ProductId, ProductId }, _store.Purchases);
        }

        [Fact]
        public async Task PurchaseAsync_WhenDeferred_ReturnsDeferredWithoutFulfillment()
        {
            var coordinator = CreateCoordinator();
            var purchase = coordinator.PurchaseAsync(ProductId, CancellationToken.None);

            _store.RaiseDeferred(ProductId);

            Assert.Equal(PurchaseOutcome.Deferred, (await purchase).Outcome);
            Assert.Empty(_fulfillment.Calls);
            Assert.Empty(_store.Confirmations);
            Assert.False(coordinator.IsPurchaseInProgress);
        }

        [Fact]
        public async Task PurchaseAsync_WhenRejected_DoesNotConfirm()
        {
            _fulfillment.Returns(FulfillmentStatus.Rejected);
            var coordinator = CreateCoordinator();
            var purchase = coordinator.PurchaseAsync(ProductId, CancellationToken.None);

            _store.RaisePending(Paid());

            Assert.Equal(PurchaseOutcome.Rejected, (await purchase).Outcome);
            Assert.Empty(_store.Confirmations);
            Assert.False(coordinator.IsPurchaseInProgress);
        }

        [Fact]
        public async Task PurchaseAsync_WhenRetryLater_DoesNotConfirmAndKeepsPurchasePending()
        {
            _fulfillment.Returns(FulfillmentStatus.RetryLater);
            var coordinator = CreateCoordinator();
            var purchase = coordinator.PurchaseAsync(ProductId, CancellationToken.None);

            _store.RaisePending(Paid());

            Assert.Equal(PurchaseOutcome.RetryLater, (await purchase).Outcome);
            Assert.Empty(_store.Confirmations);
            Assert.Equal(1, coordinator.PendingFulfillmentCount);
            Assert.False(coordinator.IsPurchaseInProgress);
        }

        [Fact]
        public async Task RetryPendingFulfillmentsAsync_WhenNowDelivered_ConfirmsOnce()
        {
            _fulfillment.Returns(FulfillmentStatus.RetryLater);
            _fulfillment.Returns(FulfillmentStatus.Delivered);
            var coordinator = CreateCoordinator();
            _store.RaisePending(Paid());

            await coordinator.RetryPendingFulfillmentsAsync(CancellationToken.None);
            await coordinator.RetryPendingFulfillmentsAsync(CancellationToken.None);

            Assert.Equal(new[] { "GPA.1" }, _store.Confirmations);
            Assert.Equal(2, _fulfillment.Calls.Count);
            Assert.Equal(0, coordinator.PendingFulfillmentCount);
        }

        [Fact]
        public void PendingPurchaseAtStartup_IsFulfilledAndConfirmedWithoutPlayerAction()
        {
            _fulfillment.Returns(FulfillmentStatus.Delivered);
            CreateCoordinator();

            _store.RaisePending(Paid());

            Assert.Equal(new[] { "GPA.1" }, _store.Confirmations);
            Assert.Contains(_completed, r => r.Outcome == PurchaseOutcome.Delivered && r.ProductId == ProductId);
        }

        [Fact]
        public void PendingPurchase_WhenRedeliveredWhileInFlight_FulfillsOnce()
        {
            CreateCoordinator();

            _store.RaisePending(Paid());
            _store.RaisePending(Paid());
            _fulfillment.Pending[0].SetResult(FulfillmentStatus.Delivered);

            Assert.Single(_fulfillment.Calls);
            Assert.Equal(new[] { "GPA.1" }, _store.Confirmations);
        }

        [Fact]
        public async Task PurchaseAsync_WhenCallerCancelsDuringFulfillment_StillConfirms()
        {
            var coordinator = CreateCoordinator();
            using var cancellation = new CancellationTokenSource();
            var purchase = coordinator.PurchaseAsync(ProductId, cancellation.Token);
            _store.RaisePending(Paid());

            cancellation.Cancel();
            var result = await purchase;
            _fulfillment.Pending[0].SetResult(FulfillmentStatus.Delivered);

            Assert.Equal(PurchaseOutcome.Cancelled, result.Outcome);
            Assert.Equal(new[] { "GPA.1" }, _store.Confirmations);
            Assert.Contains(_completed, r => r.Outcome == PurchaseOutcome.Delivered);
        }

        [Fact]
        public async Task PurchaseAsync_WhenFulfillmentThrows_ReturnsRetryLaterAndReleases()
        {
            _fulfillment.Throws(new InvalidOperationException("backend down"));
            var coordinator = CreateCoordinator();
            var purchase = coordinator.PurchaseAsync(ProductId, CancellationToken.None);

            _store.RaisePending(Paid());

            Assert.Equal(PurchaseOutcome.RetryLater, (await purchase).Outcome);
            Assert.Empty(_store.Confirmations);
            Assert.False(coordinator.IsPurchaseInProgress);
            Assert.Equal(1, coordinator.PendingFulfillmentCount);
        }

        [Fact]
        public async Task PurchaseCompleted_IsRaisedForEveryTerminalOutcome()
        {
            _fulfillment.Returns(FulfillmentStatus.Rejected);
            var coordinator = CreateCoordinator();

            var deferred = coordinator.PurchaseAsync(ProductId, CancellationToken.None);
            _store.RaiseDeferred(ProductId);
            await deferred;
            var rejected = coordinator.PurchaseAsync(ProductId, CancellationToken.None);
            _store.RaisePending(Paid());
            await rejected;

            Assert.Equal(new[] { PurchaseOutcome.Deferred, PurchaseOutcome.Rejected },
                _completed.ConvertAll(r => r.Outcome));
        }

        [Fact]
        public void StorePurchase_ToString_DoesNotContainReceipt()
        {
            var text = Paid().ToString();

            Assert.DoesNotContain("secret-receipt", text);
            Assert.Contains("GPA.1", text);
        }
    }
}
