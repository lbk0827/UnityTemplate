using System;
using System.Collections.Generic;

namespace BK.Meta
{
    public enum CurrencyKind { Plain, Rechargeable, Buff }

    /// <summary>Static description of one currency. Supplied by the game through <see cref="ICurrencyCatalog"/>.</summary>
    public sealed class CurrencyDefinition
    {
        public string Id;
        public CurrencyKind Kind = CurrencyKind.Plain;
        public long InitialValue;

        /// <summary>Rechargeable only.</summary>
        public long RechargeMax;
        public long RechargeAmount = 1;
        public TimeSpan RechargeInterval;

        /// <summary>Rechargeable only: ids of Buff currencies that make this one free while active.</summary>
        public IReadOnlyList<string> InfiniteBuffIds = Array.Empty<string>();
    }
}
