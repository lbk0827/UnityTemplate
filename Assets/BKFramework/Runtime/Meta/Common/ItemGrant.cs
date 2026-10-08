using System;

namespace BK.Meta
{
    /// <summary>A (currency id, amount) pair granted through <see cref="IWallet"/>.</summary>
    [Serializable]
    public struct ItemGrant
    {
        public string itemId;
        public long amount;

        public ItemGrant(string itemId, long amount)
        {
            this.itemId = itemId;
            this.amount = amount;
        }

        public override string ToString() => $"{itemId} x{amount}";
    }

    public static class ItemGrantExtensions
    {
        public static void GrantAll(this IWallet wallet, System.Collections.Generic.IReadOnlyList<ItemGrant> grants, string reason)
        {
            for (var i = 0; i < grants.Count; i++)
                wallet.Add(grants[i].itemId, grants[i].amount, reason);
        }
    }
}
