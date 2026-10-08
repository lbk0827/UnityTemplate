using System.Threading.Tasks;
using BK.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class DimRefCounterTests
    {
        private sealed class View : IDimView
        {
            public int Shows, Hides;
            public DimLevel Last = DimLevel.None;
            public void Show() => Shows++;
            public void Hide() => Hides++;
            public void ApplyLevel(DimLevel level) => Last = level;
        }

        private sealed class Scheduler : IDimScheduler
        {
            public UniTaskCompletionSource Grace = new(), Timeout = new();
            public UniTask GraceDelayAsync() => Grace.Task;
            public UniTask TransitionTimeoutAsync() => Timeout.Task;
        }

        [Test]
        public void AcquireManyShowsOnce()
        {
            var v = new View(); var c = new DimRefCounter(v, new Scheduler());
            c.Acquire(DimLevel.Soft); c.Acquire(DimLevel.Soft); c.Acquire(DimLevel.Soft);
            Assert.That(v.Shows, Is.EqualTo(1));
            Assert.That(c.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task ReleaseToZeroHidesAfterGrace()
        {
            var v = new View(); var s = new Scheduler(); var c = new DimRefCounter(v, s);
            c.Acquire(DimLevel.Soft); c.Release();
            Assert.That(v.Hides, Is.Zero);
            s.Grace.TrySetResult(); await c.Pending;
            Assert.That(v.Hides, Is.EqualTo(1));
        }

        [Test]
        public async Task ReacquireWithinGraceCancelsHide()
        {
            var v = new View(); var s = new Scheduler(); var c = new DimRefCounter(v, s);
            c.Acquire(DimLevel.Soft); c.Release(); c.Acquire(DimLevel.Soft);
            s.Grace.TrySetResult(); await c.Pending;
            Assert.That(v.Hides, Is.Zero);
            Assert.That(v.Shows, Is.EqualTo(1));
        }

        [Test]
        public async Task HoldForTransitionKeepsDimUntilNextAcquire()
        {
            var v = new View(); var s = new Scheduler(); var c = new DimRefCounter(v, s);
            c.Acquire(DimLevel.Soft); c.HoldForTransition(); c.Release();
            s.Grace.TrySetResult(); await UniTask.Yield();
            Assert.That(v.Hides, Is.Zero);
            c.Acquire(DimLevel.Soft);
            Assert.That(v.Shows, Is.EqualTo(1));
        }

        [Test]
        public async Task HoldTimeoutWithoutAcquireHides()
        {
            var v = new View(); var s = new Scheduler(); var c = new DimRefCounter(v, s);
            c.Acquire(DimLevel.Soft); c.HoldForTransition(); c.Release();
            s.Timeout.TrySetResult(); await c.Pending;
            Assert.That(v.Hides, Is.EqualTo(1));
        }

        [Test]
        public void ExplicitLevelWhileVisibleReappliesLevel()
        {
            var v = new View(); var c = new DimRefCounter(v, new Scheduler());
            c.Acquire(DimLevel.Soft); c.Acquire(DimLevel.Deep);
            Assert.That(v.Last, Is.EqualTo(DimLevel.Deep));
            Assert.That(v.Shows, Is.EqualTo(1));
        }

        [Test]
        public void UnbalancedReleaseIsIgnoredAndResetHides()
        {
            var v = new View(); var c = new DimRefCounter(v, new Scheduler());
            c.Release();
            Assert.That(c.Count, Is.Zero);
            c.Acquire(DimLevel.Soft); c.Acquire(DimLevel.Soft); c.Reset();
            Assert.That(c.Count, Is.Zero);
            Assert.That(v.Hides, Is.EqualTo(1));
        }
    }
}
