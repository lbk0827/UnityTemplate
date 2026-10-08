using System;
using R3;

namespace BK.Meta
{
    /// <summary>Reactive view of one currency. Mutations go through <see cref="IWallet"/>.</summary>
    public sealed class Currency : IDisposable
    {
        internal readonly ReactiveProperty<long> ValueProperty;
        internal readonly ReactiveProperty<bool> InfiniteProperty = new(false);

        public CurrencyDefinition Definition { get; }
        public ReadOnlyReactiveProperty<long> Value => ValueProperty;

        /// <summary>Rechargeable only: a linked buff is active, so entry costs are waived.</summary>
        public ReadOnlyReactiveProperty<bool> InfiniteEnabled => InfiniteProperty;

        /// <summary>Rechargeable only: start of the cycle in progress, null when full.</summary>
        public DateTime? Anchor { get; internal set; }

        /// <summary>Buff only: when the buff ends, null when inactive.</summary>
        public DateTime? BuffEnd { get; internal set; }

        internal Currency(CurrencyDefinition definition, long value)
        {
            Definition = definition;
            ValueProperty = new ReactiveProperty<long>(value);
        }

        public void Dispose()
        {
            ValueProperty.Dispose();
            InfiniteProperty.Dispose();
        }
    }
}
