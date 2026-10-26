using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.Purchasing;

namespace BK.Purchasing
{
    /// <summary>Official Unity IAP 5 Fake Store. Intentionally unavailable in players until a real store is configured.</summary>
    public sealed class UnityFakeStore : IStore
    {
        private StoreController controller;
        private Func<StorePurchase, bool> fulfill;
        private readonly Dictionary<string, PendingOrder> pending = new();
        private readonly HashSet<string> products = new();
        private List<ProductDefinition> definitions;
        private string activeProduct;
        private bool disposed, confirming;
        public bool IsReady { get; private set; }
        public bool IsBusy => activeProduct != null || pending.Count > 0;
        public event Action<string> Completed;

        public async UniTask InitializeAsync(IEnumerable<StoreProduct> catalog, Func<StorePurchase, bool> fulfill)
        {
#if UNITY_EDITOR
            if (controller != null || disposed) return;
            this.fulfill = fulfill ?? throw new ArgumentNullException(nameof(fulfill));
            definitions = catalog.Select(p => new ProductDefinition(p.Id,
                p.NonConsumable ? ProductType.NonConsumable : ProductType.Consumable)).ToList();
            controller = new StoreController("fake"); // Unity IAP's built-in FakeAppStore name.
            controller.OnStoreConnected += OnConnected;
            controller.OnStoreDisconnected += OnDisconnected;
            controller.OnProductsFetched += OnProducts;
            controller.OnProductsFetchFailed += OnProductsFailed;
            controller.OnPurchasePending += OnPending;
            controller.OnPurchaseFailed += OnFailed;
            controller.OnPurchaseConfirmed += OnConfirmed;
            controller.OnPurchaseDeferred += OnDeferred;
            // Only pending purchase callbacks can grant. Fake Store's fetched confirmed history can include cancellations.
            try { await controller.Connect(); }
            catch (Exception e) { IsReady = false; Completed?.Invoke("Store connection failed: " + e.Message); }
#else
            await UniTask.CompletedTask;
#endif
        }

        private void OnConnected() { if (!disposed) controller.FetchProducts(definitions); }
        private void OnDisconnected(StoreConnectionFailureDescription error)
        { IsReady = false; activeProduct = null; Completed?.Invoke("Store disconnected: " + error.message); }
        private void OnProducts(List<Product> fetched)
        {
            products.Clear();
            foreach (var p in fetched) if (p.availableToPurchase) products.Add(p.definition.id);
            IsReady = products.Count > 0;
        }
        private void OnProductsFailed(ProductFetchFailed error)
        { IsReady = false; Completed?.Invoke("Product loading failed: " + error.FailureReason); }

        public bool TryPurchase(string productId, out string message)
        {
            message = "";
            if (!IsReady) { message = "Fake Store is available in the Unity Editor after products finish loading."; return false; }
            if (IsBusy) { message = "A purchase or save is still pending. Please wait."; return false; }
            if (!products.Contains(productId)) { message = "Product is unavailable."; return false; }
            activeProduct = productId;
            try { controller.PurchaseProduct(productId); return true; }
            catch (Exception e) { activeProduct = null; message = e.Message; return false; }
        }

        private static string ProductId(Order order) => order.CartOrdered.Items().FirstOrDefault()?.Product.definition.id;
        private void OnPending(PendingOrder order)
        {
            var items = order.CartOrdered.Items();
            var transaction = order.Info.TransactionID;
            if (items.Count != 1 || string.IsNullOrEmpty(transaction))
            { Completed?.Invoke("Unsupported purchase. The order has not been confirmed."); return; }
            pending[transaction] = order;
            RetryPending();
        }
        public void RetryPending()
        {
            if (disposed || confirming) return;
            confirming = true;
            try
            {
                foreach (var pair in pending.ToArray())
                {
                    if (!fulfill(new StorePurchase(ProductId(pair.Value), pair.Key))) continue;
                    controller.ConfirmPurchase(pair.Value);
                }
            }
            catch (Exception e) { Completed?.Invoke("Purchase is waiting for recovery: " + e.Message); }
            finally { confirming = false; }
        }
        private void OnConfirmed(Order order)
        {
            if (order is FailedOrder failure)
            { Completed?.Invoke("Purchase confirmation will be retried: " + failure.FailureReason); return; }
            pending.Remove(order.Info.TransactionID);
            if (ProductId(order) == activeProduct) activeProduct = null;
            Completed?.Invoke("Test purchase complete. Rewards have been saved; no payment was taken.");
        }
        private void OnFailed(FailedOrder order)
        {
            if (ProductId(order) != activeProduct) return;
            activeProduct = null;
            Completed?.Invoke(order.FailureReason == PurchaseFailureReason.UserCancelled
                ? "Purchase cancelled. No rewards were granted." : "Purchase failed: " + order.FailureReason);
        }
        private void OnDeferred(DeferredOrder order)
        { Completed?.Invoke("Purchase is awaiting approval."); }

        public void Dispose()
        {
            disposed = true; IsReady = false;
            if (controller == null) return;
            controller.OnStoreConnected -= OnConnected;
            controller.OnStoreDisconnected -= OnDisconnected;
            controller.OnProductsFetched -= OnProducts;
            controller.OnProductsFetchFailed -= OnProductsFailed;
            controller.OnPurchasePending -= OnPending;
            controller.OnPurchaseFailed -= OnFailed;
            controller.OnPurchaseConfirmed -= OnConfirmed;
            controller.OnPurchaseDeferred -= OnDeferred;
            pending.Clear(); Completed = null;
        }
    }
}
