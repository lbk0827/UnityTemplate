using System;
using System.Collections.Generic;
using R3;
using VContainer.Unity;
using BK.Core.Time;
using BK.Save;

namespace BK.Meta
{
    /// <inheritdoc cref="IWallet"/>
    public sealed class Wallet : IWallet, IDisposable
    {
        private readonly Dictionary<string, Currency> _currencies = new();
        private readonly WalletData _data;
        private readonly IClock _clock;
        private readonly Subject<WalletChange> _changed = new();

        public Wallet(ICurrencyCatalog catalog, ISaveService saves, IClock clock)
        {
            _clock = clock;
            _data = saves.Get<WalletData>();
            var now = clock.UtcNow;

            foreach (var definition in catalog.Definitions)
            {
                var entry = _data.Find(definition.Id);
                if (entry == null)
                {
                    entry = new WalletData.Entry { id = definition.Id, value = definition.InitialValue };
                    _data.entries.Add(entry);
                    _data.MarkDirty();
                }

                DateTime? stamp = entry.stampTicks == 0 ? null : new DateTime(entry.stampTicks, DateTimeKind.Utc);
                var currency = new Currency(definition, entry.value);
                switch (definition.Kind)
                {
                    case CurrencyKind.Buff:
                        currency.BuffEnd = stamp;
                        break;
                    case CurrencyKind.Rechargeable:
                        var state = RechargeLogic.OnLoad(new RechargeLogic.State(entry.value, stamp), definition, now);
                        currency.Anchor = state.Anchor;
                        if (state.Anchor != stamp)
                            Persist(currency, state.Value, state.Anchor);
                        break;
                }
                _currencies.Add(definition.Id, currency);
            }

            Tick(now);
        }

        public Observable<WalletChange> Changed => _changed;

        public Currency Get(string id)
            => _currencies.TryGetValue(id, out var currency)
                ? currency
                : throw new KeyNotFoundException($"Currency '{id}' is not in the catalog.");

        public long ValueOf(string id) => Get(id).ValueProperty.Value;
        public bool CanAfford(string id, long amount) => ValueOf(id) >= amount;
        public bool IsInfinite(string id) => Get(id).InfiniteProperty.Value;

        public TimeSpan TimeToNext(string id)
        {
            var currency = Get(id);
            return RechargeLogic.TimeToNext(new RechargeLogic.State(currency.ValueProperty.Value, currency.Anchor), currency.Definition, _clock.UtcNow);
        }

        public void Add(string id, long delta, string reason)
        {
            var current = ValueOf(id);
            Apply(id, Math.Max(0L, current + delta), reason);
        }

        public void Set(string id, long value, string reason) => Apply(id, Math.Max(0L, value), reason);

        private void Apply(string id, long value, string reason)
        {
            var currency = Get(id);
            var now = _clock.UtcNow;
            var delta = value - currency.ValueProperty.Value;

            if (currency.Definition.Kind == CurrencyKind.Buff)
            {
                currency.BuffEnd = value > 0 ? now + TimeSpan.FromSeconds(value) : null;
                Persist(currency, value, currency.BuffEnd);
                RefreshInfinite(now);
            }
            else
            {
                var state = RechargeLogic.SetValue(new RechargeLogic.State(currency.ValueProperty.Value, currency.Anchor), currency.Definition, value, now);
                currency.Anchor = state.Anchor;
                Persist(currency, state.Value, state.Anchor);
            }

            _changed.OnNext(new WalletChange(id, delta, reason));
        }

        public void Tick(DateTime now)
        {
            foreach (var currency in _currencies.Values)
            {
                switch (currency.Definition.Kind)
                {
                    case CurrencyKind.Buff:
                    {
                        var remain = currency.BuffEnd.HasValue ? (long)Math.Max(0d, (currency.BuffEnd.Value - now).TotalSeconds) : 0L;
                        if (remain == 0 && currency.BuffEnd.HasValue)
                            currency.BuffEnd = null;
                        if (remain != currency.ValueProperty.Value)
                            Persist(currency, remain, currency.BuffEnd);
                        break;
                    }
                    case CurrencyKind.Rechargeable:
                    {
                        var state = RechargeLogic.Advance(new RechargeLogic.State(currency.ValueProperty.Value, currency.Anchor), currency.Definition, now);
                        if (state.Value != currency.ValueProperty.Value || state.Anchor != currency.Anchor)
                        {
                            currency.Anchor = state.Anchor;
                            Persist(currency, state.Value, state.Anchor);
                        }
                        break;
                    }
                }
            }

            RefreshInfinite(now);
        }

        private void RefreshInfinite(DateTime now)
        {
            foreach (var currency in _currencies.Values)
            {
                if (currency.Definition.Kind != CurrencyKind.Rechargeable)
                    continue;

                var active = false;
                foreach (var buffId in currency.Definition.InfiniteBuffIds)
                {
                    if (_currencies.TryGetValue(buffId, out var buff) && buff.BuffEnd.HasValue && buff.BuffEnd.Value > now)
                    {
                        active = true;
                        break;
                    }
                }

                currency.InfiniteProperty.Value = active;
            }
        }

        private void Persist(Currency currency, long value, DateTime? stamp)
        {
            currency.ValueProperty.Value = value;
            var entry = _data.Find(currency.Definition.Id);
            entry.value = value;
            entry.stampTicks = stamp?.Ticks ?? 0L;
            _data.MarkDirty();
        }

        public void Dispose()
        {
            foreach (var currency in _currencies.Values)
                currency.Dispose();
            _changed.Dispose();
        }
    }

    /// <summary>Drives recharge and buff expiry once per frame.</summary>
    public sealed class WalletTicker : ITickable
    {
        private readonly IWallet _wallet;
        private readonly IClock _clock;

        public WalletTicker(IWallet wallet, IClock clock)
        {
            _wallet = wallet;
            _clock = clock;
        }

        public void Tick() => _wallet.Tick(_clock.UtcNow);
    }
}
