#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Purchasing;

namespace DevilCorp.MobileFoundation.Purchases.UnityIap
{
    /// <summary>
    /// Unity IAP 5 implementation of <see cref="IStoreGateway"/>. It only translates the SDK: validation and
    /// granting belong to <see cref="IPurchaseFulfillment"/>, and confirmation happens when the coordinator asks.
    /// </summary>
    public sealed class UnityIapStoreGateway : IStoreGateway, IDisposable
    {
        private readonly IReadOnlyList<StoreProductDefinition> _catalog;
        private readonly Action<string> _log;
        private readonly Dictionary<string, PendingOrder> _pendingOrders = new Dictionary<string, PendingOrder>();
        private StoreController? _store;
        private bool _productsFetched;

        public UnityIapStoreGateway(IReadOnlyList<StoreProductDefinition> catalog, Action<string>? log = null)
        {
            _catalog = catalog;
            _log = log ?? (_ => { });
        }

        public event Action<StorePurchase>? PurchasePending;
        public event Action<string, StoreFailure>? PurchaseFailed;
        public event Action<string>? PurchaseDeferred;

        public bool IsReady => _store != null && _productsFetched;

        /// <summary>
        /// Connects to the platform store. Every event is subscribed first: unconfirmed purchases from a
        /// previous session can be delivered as soon as the store connects.
        /// </summary>
        public async Task ConnectAsync()
        {
            if (_store != null)
            {
                return;
            }

            _store = UnityIAPServices.StoreController();
            _store.OnStoreConnected += HandleStoreConnected;
            _store.OnStoreDisconnected += HandleStoreDisconnected;
            _store.OnProductsFetched += HandleProductsFetched;
            _store.OnProductsFetchFailed += HandleProductsFetchFailed;
            _store.OnPurchasesFetched += HandlePurchasesFetched;
            _store.OnPurchasesFetchFailed += HandlePurchasesFetchFailed;
            _store.OnPurchasePending += HandlePurchasePending;
            _store.OnPurchaseConfirmed += HandlePurchaseConfirmed;
            _store.OnPurchaseFailed += HandlePurchaseFailed;
            _store.OnPurchaseDeferred += HandlePurchaseDeferred;

            await _store.Connect();
        }

        public void Purchase(string productId)
        {
            var product = _store?.GetProductById(productId);
            if (_store == null || product == null || !product.availableToPurchase)
            {
                PurchaseFailed?.Invoke(productId, StoreFailure.Unavailable);
                return;
            }

            _store.PurchaseProduct(product);
        }

        public void Confirm(string transactionId)
        {
            if (_store == null || !_pendingOrders.TryGetValue(transactionId, out var order))
            {
                _log($"No pending order to confirm for transaction {transactionId}");
                return;
            }

            _pendingOrders.Remove(transactionId);
            _store.ConfirmPurchase(order);
        }

        public void Reconnect()
        {
            if (_store == null || _productsFetched)
            {
                return;
            }

            // IAP 5 accepts a new Connect once the previous attempt completed; OnStoreConnected refetches products.
            _ = _store.Connect();
        }

        public void Dispose()
        {
            if (_store == null)
            {
                return;
            }

            _store.OnStoreConnected -= HandleStoreConnected;
            _store.OnStoreDisconnected -= HandleStoreDisconnected;
            _store.OnProductsFetched -= HandleProductsFetched;
            _store.OnProductsFetchFailed -= HandleProductsFetchFailed;
            _store.OnPurchasesFetched -= HandlePurchasesFetched;
            _store.OnPurchasesFetchFailed -= HandlePurchasesFetchFailed;
            _store.OnPurchasePending -= HandlePurchasePending;
            _store.OnPurchaseConfirmed -= HandlePurchaseConfirmed;
            _store.OnPurchaseFailed -= HandlePurchaseFailed;
            _store.OnPurchaseDeferred -= HandlePurchaseDeferred;
        }

        private void HandleStoreConnected()
        {
            var catalogProvider = new CatalogProvider();
            foreach (var definition in _catalog)
            {
                catalogProvider.AddProduct(definition.ProductId, definition.Type, new StoreSpecificIds
                {
                    { definition.GooglePlayId, GooglePlay.Name },
                    { definition.AppleAppStoreId, AppleAppStore.Name }
                });
            }

            catalogProvider.FetchProducts(products => _store?.FetchProducts(products));
        }

        private void HandleStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            _productsFetched = false;
            _log($"Store disconnected: {failure.Message}");
        }

        private void HandleProductsFetched(List<Product> products)
        {
            _productsFetched = true;
            _log($"Store products fetched: {products.Count}");

            // Re-delivers purchases left unconfirmed by a previous session through OnPurchasePending.
            _store?.FetchPurchases();
        }

        private void HandleProductsFetchFailed(ProductFetchFailed failure)
        {
            _log($"Store products fetch failed: {failure.FailureReason}");
        }

        private void HandlePurchasesFetched(Orders orders)
        {
            _log("Store purchases fetched");
        }

        private void HandlePurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            _log($"Store purchases fetch failed: {failure.FailureReason}");
        }

        private void HandlePurchasePending(PendingOrder order)
        {
            var transactionId = order.Info.TransactionID;
            _pendingOrders[transactionId] = order;
            PurchasePending?.Invoke(new StorePurchase(ProductIdOf(order), transactionId, order.Info.Receipt));
        }

        private void HandlePurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed)
            {
                _log($"Store confirmation failed for {ProductIdOf(order)}: {failed.FailureReason}");
            }
        }

        private void HandlePurchaseFailed(FailedOrder order)
        {
            PurchaseFailed?.Invoke(ProductIdOf(order), ToStoreFailure(order.FailureReason));
        }

        private void HandlePurchaseDeferred(DeferredOrder order)
        {
            PurchaseDeferred?.Invoke(ProductIdOf(order));
        }

        private static string ProductIdOf(Order order)
        {
            var items = order.CartOrdered?.Items();
            return items != null && items.Count > 0 ? items[0].Product?.definition?.id ?? string.Empty : string.Empty;
        }

        private static StoreFailure ToStoreFailure(PurchaseFailureReason reason)
        {
            switch (reason)
            {
                case PurchaseFailureReason.UserCancelled:
                case PurchaseFailureReason.OrderCancelled:
                    return StoreFailure.Cancelled;
                case PurchaseFailureReason.PurchasingUnavailable:
                case PurchaseFailureReason.ProductUnavailable:
                case PurchaseFailureReason.StoreNotConnected:
                case PurchaseFailureReason.NotSupported:
                case PurchaseFailureReason.UserNotAuthenticated:
                    return StoreFailure.Unavailable;
                default:
                    return StoreFailure.Failed;
            }
        }
    }
}
