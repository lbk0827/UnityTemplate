using System;
using System.Collections.Generic;
using System.Linq;
using BK.Save;

namespace BK.Meta
{
    /// <summary>Local purchase deduplication. Receipt and granted balances are persisted in the same WalletData slot.</summary>
    public sealed class PurchaseRewards
    {
        private readonly IWallet wallet;
        private readonly WalletData data;
        public PurchaseRewards(IWallet wallet, ISaveService saves)
        { this.wallet = wallet; data = saves.Get<WalletData>(); data.purchases ??= new(); }
        public IReadOnlyList<WalletData.PurchaseReceipt> Receipts => data.purchases;
        public int Count(string productId) => data.purchases.Count(p => p.productId == productId);
        public WalletData.PurchaseReceipt Find(string transactionId) => data.purchases.Find(p => p.transactionId == transactionId);

        public bool Grant(string transactionId, string productId, string context, IReadOnlyList<ItemGrant> rewards)
        {
            if (string.IsNullOrEmpty(transactionId) || string.IsNullOrEmpty(productId)) throw new ArgumentException("Purchase identity is required.");
            var previous = Find(transactionId);
            if (previous != null)
            {
                if (previous.productId != productId) throw new InvalidOperationException("Receipt product mismatch.");
                return false;
            }
            // Validate the entire basket before mutating any currency.
            foreach (var reward in rewards)
            {
                if (reward.amount <= 0) throw new ArgumentException("Purchase rewards must be positive.");
                wallet.Get(reward.itemId);
            }
            wallet.GrantAll(rewards, "store_purchase");
            data.purchases.Add(new WalletData.PurchaseReceipt { transactionId = transactionId, productId = productId, context = context });
            data.MarkDirty();
            return true;
        }
    }
}
