using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace BK.Purchasing
{
    public readonly struct StoreProduct
    {
        public readonly string Id;
        public readonly bool NonConsumable;
        public StoreProduct(string id, bool nonConsumable = false) { Id = id; NonConsumable = nonConsumable; }
    }

    public readonly struct StorePurchase
    {
        public readonly string ProductId, TransactionId;
        public StorePurchase(string productId, string transactionId) { ProductId = productId; TransactionId = transactionId; }
    }

    /// <summary>The game persists rewards before returning true from fulfill. False keeps the order pending.</summary>
    public interface IStore : IDisposable
    {
        bool IsReady { get; }
        bool IsBusy { get; }
        event Action<string> Completed;
        UniTask InitializeAsync(IEnumerable<StoreProduct> products, Func<StorePurchase, bool> fulfill);
        bool TryPurchase(string productId, out string message);
        void RetryPending();
    }
}
