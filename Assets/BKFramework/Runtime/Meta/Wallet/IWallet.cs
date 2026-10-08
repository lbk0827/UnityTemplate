using System;
using R3;

namespace BK.Meta
{
    public readonly struct WalletChange
    {
        public readonly string CurrencyId;
        public readonly long Delta;
        public readonly string Reason;

        public WalletChange(string currencyId, long delta, string reason)
        {
            CurrencyId = currencyId;
            Delta = delta;
            Reason = reason;
        }
    }

    /// <summary>
    /// All player currencies: plain balances, rechargeable ones (hearts) and timed buffs.
    /// Every mutation persists through BK.Save and publishes a <see cref="WalletChange"/>.
    /// </summary>
    public interface IWallet
    {
        Currency Get(string id);
        long ValueOf(string id);
        bool CanAfford(string id, long amount);

        /// <summary>Adds (or subtracts, clamped at zero). For Buff currencies the amount is seconds added to the remaining time.</summary>
        void Add(string id, long delta, string reason);

        /// <summary>Sets the value. Buff: seconds from now; 0 clears the buff.</summary>
        void Set(string id, long value, string reason);

        /// <summary>True when a rechargeable currency is free right now (a linked buff is active).</summary>
        bool IsInfinite(string id);

        TimeSpan TimeToNext(string id);

        /// <summary>Advances recharge and buff expiry to <paramref name="now"/>. Driven by the ticker; safe to call any time.</summary>
        void Tick(DateTime now);

        Observable<WalletChange> Changed { get; }
    }
}
