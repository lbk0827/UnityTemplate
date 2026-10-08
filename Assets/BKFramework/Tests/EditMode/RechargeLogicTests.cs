using System;
using BK.Meta;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class RechargeLogicTests
    {
        private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly CurrencyDefinition Heart = new()
        {
            Id = "Heart", Kind = CurrencyKind.Rechargeable, InitialValue = 5,
            RechargeMax = 5, RechargeAmount = 1, RechargeInterval = TimeSpan.FromMinutes(30),
        };

        private static TimeSpan Min(int m) => TimeSpan.FromMinutes(m);

        [Test]
        public void AdvanceAppliesWholeIntervalsAndKeepsFraction()
        {
            var s = RechargeLogic.Advance(new RechargeLogic.State(2, T0), Heart, T0 + Min(75));
            Assert.That(s.Value, Is.EqualTo(4));
            Assert.That(s.Anchor, Is.EqualTo(T0 + Min(60)));
        }

        [Test]
        public void AdvanceClampsToMaxAndClearsAnchor()
        {
            var s = RechargeLogic.Advance(new RechargeLogic.State(4, T0), Heart, T0 + Min(200));
            Assert.That(s.Value, Is.EqualTo(5));
            Assert.That(s.Anchor, Is.Null);
        }

        [Test]
        public void AdvanceNoOpWhenFullOrNoAnchorOrTooEarly()
        {
            var full = RechargeLogic.Advance(new RechargeLogic.State(5, null), Heart, T0 + Min(500));
            Assert.That(full.Value, Is.EqualTo(5));
            var noAnchor = RechargeLogic.Advance(new RechargeLogic.State(3, null), Heart, T0 + Min(500));
            Assert.That(noAnchor.Value, Is.EqualTo(3));
            var early = RechargeLogic.Advance(new RechargeLogic.State(3, T0), Heart, T0 + Min(29));
            Assert.That(early.Value, Is.EqualTo(3));
            Assert.That(early.Anchor, Is.EqualTo(T0));
        }

        [Test]
        public void SpendBelowMaxKeepsAnchor_SpendFromFullStartsCycle()
        {
            var fromFull = RechargeLogic.SetValue(new RechargeLogic.State(5, null), Heart, 4, T0 + Min(10));
            Assert.That(fromFull.Anchor, Is.EqualTo(T0 + Min(10)));
            var running = RechargeLogic.SetValue(new RechargeLogic.State(4, T0), Heart, 3, T0 + Min(10));
            Assert.That(running.Anchor, Is.EqualTo(T0));
            var refill = RechargeLogic.SetValue(new RechargeLogic.State(4, T0), Heart, 5, T0 + Min(10));
            Assert.That(refill.Anchor, Is.Null);
        }

        [Test]
        public void OnLoadGivesAnchorToNonFullValueWithoutOne()
        {
            var s = RechargeLogic.OnLoad(new RechargeLogic.State(2, null), Heart, T0);
            Assert.That(s.Anchor, Is.EqualTo(T0));
            var keep = RechargeLogic.OnLoad(new RechargeLogic.State(2, T0 - Min(5)), Heart, T0);
            Assert.That(keep.Anchor, Is.EqualTo(T0 - Min(5)));
            var full = RechargeLogic.OnLoad(new RechargeLogic.State(5, null), Heart, T0);
            Assert.That(full.Anchor, Is.Null);
        }

        [Test]
        public void TimeToNextAndFullAt()
        {
            var s = new RechargeLogic.State(2, T0);
            Assert.That(RechargeLogic.TimeToNext(s, Heart, T0 + Min(10)), Is.EqualTo(Min(20)));
            Assert.That(RechargeLogic.FullAt(s, Heart), Is.EqualTo(T0 + Min(90)));
            var full = new RechargeLogic.State(5, null);
            Assert.That(RechargeLogic.TimeToNext(full, Heart, T0), Is.EqualTo(TimeSpan.Zero));
            Assert.That(RechargeLogic.FullAt(full, Heart), Is.Null);
        }
    }
}
