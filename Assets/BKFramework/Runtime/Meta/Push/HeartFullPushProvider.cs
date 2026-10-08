using System;
using System.Collections.Generic;
using BK.Notifications;

namespace BK.Meta
{
    /// <summary>"Your hearts are full" at the moment the rechargeable currency fills up. Nothing while full or under an infinite buff.</summary>
    public sealed class HeartFullPushProvider : IPushProvider
    {
        private readonly IWallet _wallet;
        private readonly string _currencyId;
        private readonly int _id;
        private readonly string _title;
        private readonly string _body;

        public HeartFullPushProvider(IWallet wallet, string currencyId, int id, string title, string body)
        {
            _wallet = wallet;
            _currencyId = currencyId;
            _id = id;
            _title = title;
            _body = body;
        }

        public IEnumerable<NotificationRequest> Build(DateTime nowLocal)
        {
            var currency = _wallet.Get(_currencyId);
            if (currency.Definition.Kind != CurrencyKind.Rechargeable || currency.InfiniteEnabled.CurrentValue)
                yield break;
            var fullAt = RechargeLogic.FullAt(new RechargeLogic.State(currency.Value.CurrentValue, currency.Anchor), currency.Definition);
            if (fullAt == null)
                yield break;
            yield return new NotificationRequest(_id, _title, _body, DateTime.SpecifyKind(fullAt.Value, DateTimeKind.Utc).ToLocalTime());
        }
    }
}
