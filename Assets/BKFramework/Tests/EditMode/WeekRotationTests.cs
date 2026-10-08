using System;
using BK.Meta;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class WeekRotationTests
    {
        private static readonly DateTime Epoch = new(2026, 1, 2, 7, 0, 0, DateTimeKind.Utc);

        [Test]
        public void IndexBoundaries()
        {
            var r = new WeekRotation(Epoch);
            Assert.That(r.IndexOf(Epoch - TimeSpan.FromTicks(1)), Is.EqualTo(-1));
            Assert.That(r.IndexOf(Epoch), Is.Zero);
            Assert.That(r.IndexOf(Epoch + TimeSpan.FromDays(7) - TimeSpan.FromTicks(1)), Is.Zero);
            Assert.That(r.IndexOf(Epoch + TimeSpan.FromDays(7)), Is.EqualTo(1));
            Assert.That(r.StartOf(2), Is.EqualTo(Epoch + TimeSpan.FromDays(14)));
            Assert.That(r.EndOf(2), Is.EqualTo(Epoch + TimeSpan.FromDays(21)));
        }

        [Test]
        public void FormatRemainingMatchesSpec()
        {
            Assert.That(WeekRotation.FormatRemaining(new TimeSpan(2, 5, 30, 0)), Is.EqualTo("2d 5h"));
            Assert.That(WeekRotation.FormatRemaining(new TimeSpan(0, 23, 59, 59)), Is.EqualTo("23h 59m"));
            Assert.That(WeekRotation.FormatRemaining(new TimeSpan(0, 0, 59, 5)), Is.EqualTo("59m 5s"));
            Assert.That(WeekRotation.FormatRemaining(TimeSpan.FromSeconds(-5)), Is.EqualTo("0m 0s"));
        }
    }
}
