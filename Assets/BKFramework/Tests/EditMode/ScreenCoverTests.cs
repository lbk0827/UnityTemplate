using System.Threading;
using System.Threading.Tasks;
using BK.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class ScreenCoverTests
    {
        private sealed class View : ICoverView
        {
            public bool Visible;
            public float Alpha;
            public int Fades;
            public void SetVisible(bool visible) => Visible = visible;
            public void SetAlpha(float alpha) => Alpha = alpha;
            public UniTask FadeAsync(float to, float seconds, CancellationToken ct) { Fades++; Alpha = to; return UniTask.CompletedTask; }
        }

        private sealed class Ready : IRevealReady
        {
            public readonly UniTaskCompletionSource Source = new();
            public bool IsReady => Source.Task.Status.IsCompleted();
            public UniTask ReadyTask => Source.Task;
        }

        [Test]
        public async Task HoldShowsAndReleaseHidesAfterFade()
        {
            var v = new View(); var c = new ScreenCover(v, fadeOutSeconds: 0.1f, readyTimeoutSeconds: 1f);
            c.Hold();
            Assert.That(c.IsActive, Is.True);
            Assert.That(v.Visible, Is.True);
            Assert.That(v.Alpha, Is.EqualTo(1f));
            var waited = false;
            var wait = c.WaitUntilReleasedAsync().ContinueWith(() => waited = true);
            await c.ReleaseAsync();
            await wait;
            Assert.That(waited, Is.True);
            Assert.That(c.IsActive, Is.False);
            Assert.That(v.Visible, Is.False);
            Assert.That(v.Fades, Is.EqualTo(1));
        }

        [Test]
        public async Task ReleaseWhenReadyWaitsForRegisteredReady()
        {
            var v = new View(); var c = new ScreenCover(v, 0f, readyTimeoutSeconds: 5f);
            var ready = new Ready(); c.RegisterRevealReady(ready);
            c.Hold();
            var release = c.ReleaseWhenReadyAsync();
            await UniTask.Yield();
            Assert.That(c.IsActive, Is.True, "still held until ready");
            ready.Source.TrySetResult();
            await release;
            Assert.That(c.IsActive, Is.False);
        }

        [Test]
        public async Task ReleaseWhenReadyTimesOutWithoutReady()
        {
            var v = new View(); var c = new ScreenCover(v, 0f, readyTimeoutSeconds: 0.05f);
            c.RegisterRevealReady(new Ready());
            c.Hold();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("reveal ready timed out"));
            await c.ReleaseWhenReadyAsync();
            Assert.That(c.IsActive, Is.False);
        }

        [Test]
        public async Task ReleaseWithoutHoldIsNoOpAndUnregisterClearsReady()
        {
            var v = new View(); var c = new ScreenCover(v, 0f, 1f);
            await c.ReleaseAsync();
            Assert.That(v.Fades, Is.Zero);
            var ready = new Ready(); c.RegisterRevealReady(ready); c.UnregisterRevealReady(ready);
            c.Hold();
            await c.ReleaseWhenReadyAsync();   // no ready registered: fallback path
            Assert.That(c.IsActive, Is.False);
        }
    }
}
