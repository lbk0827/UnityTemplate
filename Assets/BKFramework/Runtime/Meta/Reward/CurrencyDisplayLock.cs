using System;
using System.Collections.Generic;
using R3;

namespace BK.Meta
{
    /// <summary>
    /// Lets the wallet be credited immediately while the HUD keeps showing the old value
    /// until the arrival effect lands. HUD widgets read through <see cref="GetDisplayValueOr"/>.
    /// </summary>
    public sealed class CurrencyDisplayLock : IDisposable
    {
        private readonly Dictionary<string, long> _locked = new();
        private readonly Subject<string> _changed = new();

        /// <summary>Emits the currency id after Engage and after Release. The dictionary is updated before the emit.</summary>
        public Observable<string> Changed => _changed;

        public void Engage(string currencyId, long displayValue)
        {
            _locked[currencyId] = displayValue;
            _changed.OnNext(currencyId);
        }

        public void Release(string currencyId)
        {
            if (!_locked.Remove(currencyId))
                return;
            _changed.OnNext(currencyId);
        }

        public bool TryGetLockedValue(string currencyId, out long value) => _locked.TryGetValue(currencyId, out value);

        public long GetDisplayValueOr(string currencyId, long fallback)
            => _locked.TryGetValue(currencyId, out var value) ? value : fallback;

        public void Dispose() => _changed.Dispose();
    }
}
