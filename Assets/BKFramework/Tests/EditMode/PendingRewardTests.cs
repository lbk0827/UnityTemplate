using System.Collections.Generic;
using BK.Meta;
using NUnit.Framework;
using R3;

namespace BK.Tests
{
    public sealed class PendingRewardTests
    {
        [Test]
        public void CtorDefaultsAndQueueOrder()
        {
            var a = new PendingReward("Gold", 50, 100, 150);
            Assert.That(a.AlreadyCredited, Is.False);
            Assert.That(a.Kind, Is.EqualTo(PendingRewardKind.Claimed));
            var b = new PendingReward("Gold", 100, 150, 250, PendingRewardKind.ClaimedDouble, alreadyCredited: true);
            Assert.That(b.AlreadyCredited, Is.True);

            var queue = new PendingRewardQueue();
            Assert.That(queue.HasPending, Is.False);
            Assert.That(queue.TryDequeue(out _), Is.False);
            queue.Enqueue(a); queue.Enqueue(b);
            Assert.That(queue.Count, Is.EqualTo(2));
            Assert.That(queue.TryDequeue(out var first) && first.Amount == 50, Is.True);
            queue.Clear();
            Assert.That(queue.HasPending, Is.False);
        }
    }

    public sealed class CurrencyDisplayLockTests
    {
        [Test]
        public void EngageLocksReleaseClearsAndIsIdempotent()
        {
            using var l = new CurrencyDisplayLock();
            var events = new List<string>();
            using var sub = l.Changed.Subscribe(events.Add);

            Assert.That(l.TryGetLockedValue("Gold", out _), Is.False);
            l.Engage("Gold", 100);
            Assert.That(l.TryGetLockedValue("Gold", out var v) && v == 100, Is.True);
            Assert.That(l.GetDisplayValueOr("Gold", 999), Is.EqualTo(100));
            l.Engage("Gold", 120);
            Assert.That(l.GetDisplayValueOr("Gold", 999), Is.EqualTo(120), "re-engage overwrites");
            l.Release("Gold");
            Assert.That(l.GetDisplayValueOr("Gold", 999), Is.EqualTo(999));
            l.Release("Gold");
            l.Release("Gem");
            Assert.That(events, Is.EqualTo(new[] { "Gold", "Gold", "Gold" }), "no emit for no-op releases");
        }

        [Test]
        public void ChangedCallbackSeesDictionaryAfterUpdate()
        {
            using var l = new CurrencyDisplayLock();
            long seen = -1;
            using var sub = l.Changed.Subscribe(id => seen = l.GetDisplayValueOr(id, -2));
            l.Engage("Gold", 7);
            Assert.That(seen, Is.EqualTo(7));
            l.Release("Gold");
            Assert.That(seen, Is.EqualTo(-2));
        }
    }
}
