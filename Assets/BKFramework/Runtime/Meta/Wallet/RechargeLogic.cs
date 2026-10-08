using System;

namespace BK.Meta
{
    /// <summary>Rechargeable currency math. State in, state out; no clocks, no Unity.</summary>
    public static class RechargeLogic
    {
        public readonly struct State
        {
            public readonly long Value;

            /// <summary>Start of the cycle currently in progress. Null when full.</summary>
            public readonly DateTime? Anchor;

            public State(long value, DateTime? anchor)
            {
                Value = value;
                Anchor = anchor;
            }
        }

        /// <summary>Applies every whole interval elapsed since the anchor. This is also the offline catch-up.</summary>
        public static State Advance(State s, CurrencyDefinition d, DateTime now)
        {
            if (d.Kind != CurrencyKind.Rechargeable || s.Value >= d.RechargeMax || s.Anchor == null
                || d.RechargeInterval <= TimeSpan.Zero || d.RechargeAmount <= 0)
                return s;

            var intervals = (long)Math.Floor((now - s.Anchor.Value).TotalSeconds / d.RechargeInterval.TotalSeconds);
            if (intervals <= 0)
                return s;

            var value = Math.Min(s.Value + intervals * d.RechargeAmount, d.RechargeMax);
            DateTime? anchor = s.Anchor.Value + TimeSpan.FromTicks(d.RechargeInterval.Ticks * intervals);
            if (value >= d.RechargeMax)
                anchor = null;
            return new State(value, anchor);
        }

        /// <summary>Anchor rules when the value is set directly (spend, refund, purchase).</summary>
        public static State SetValue(State s, CurrencyDefinition d, long newValue, DateTime now)
        {
            if (d.Kind != CurrencyKind.Rechargeable)
                return new State(newValue, null);
            if (newValue >= d.RechargeMax)
                return new State(newValue, null);
            if (s.Value >= d.RechargeMax)
                return new State(newValue, now);          // crossed from full to not full: a new cycle starts now
            return new State(newValue, s.Anchor ?? now);  // keep the running cycle
        }

        /// <summary>Normalises a loaded state: a non-full value always has an anchor.</summary>
        public static State OnLoad(State s, CurrencyDefinition d, DateTime now)
            => d.Kind == CurrencyKind.Rechargeable && s.Value < d.RechargeMax && s.Anchor == null
                ? new State(s.Value, now)
                : s;

        public static TimeSpan TimeToNext(State s, CurrencyDefinition d, DateTime now)
        {
            if (s.Value >= d.RechargeMax || s.Anchor == null)
                return TimeSpan.Zero;
            var remaining = d.RechargeInterval - (now - s.Anchor.Value);
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public static DateTime? FullAt(State s, CurrencyDefinition d)
        {
            if (s.Value >= d.RechargeMax || s.Anchor == null || d.RechargeAmount <= 0)
                return null;
            var cycles = (long)Math.Ceiling((d.RechargeMax - s.Value) / (double)d.RechargeAmount);
            return s.Anchor.Value + TimeSpan.FromTicks(d.RechargeInterval.Ticks * cycles);
        }
    }
}
