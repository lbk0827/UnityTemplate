using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BK.Meta;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BK.Tests
{
    public sealed class StagePreloaderRegistryTests
    {
        private sealed class Preloader : IStagePreloader
        {
            public readonly List<int> Requested = new();
            public UniTaskCompletionSource Gate = new();
            public bool Cancelled;

            public async UniTask PreloadStageAsync(int stageNumber, CancellationToken cancellationToken)
            {
                Requested.Add(stageNumber);
                try { await Gate.Task.AttachExternalCancellation(cancellationToken); }
                catch (System.OperationCanceledException) { Cancelled = true; throw; }
            }
        }

        [Test]
        public void RegisterStartInvokesPreloaderAndLastRegistrationWins()
        {
            var a = new Preloader(); var b = new Preloader();
            using var registry = new StagePreloaderRegistry();
            registry.Register(a); registry.Register(b);
            registry.StartPreload(3);
            Assert.That(b.Requested, Is.EqualTo(new[] { 3 }));
            Assert.That(a.Requested, Is.Empty);
            Assert.That(registry.IsPreloading, Is.True);
        }

        [Test]
        public void UnregisterOnlyClearsMatchingReference()
        {
            var a = new Preloader(); var b = new Preloader();
            using var registry = new StagePreloaderRegistry();
            registry.Register(a);
            registry.Unregister(b);
            registry.StartPreload(1);
            Assert.That(a.Requested, Is.EqualTo(new[] { 1 }));
            registry.Unregister(a);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no stage preloader registered"));
            registry.StartPreload(2);
            Assert.That(a.Requested, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public async Task SecondStartCancelsFirstAndKeepsIsPreloadingForTheNewRun()
        {
            var p = new Preloader();
            using var registry = new StagePreloaderRegistry();
            registry.Register(p);
            registry.StartPreload(1);
            registry.StartPreload(2);
            await UniTask.Yield();
            Assert.That(p.Cancelled, Is.True, "first run observed cancellation");
            Assert.That(registry.IsPreloading, Is.True, "second run is still in flight");
            p.Gate.TrySetResult();
            await UniTask.Yield();
            Assert.That(registry.IsPreloading, Is.False);
        }

        [Test]
        public async Task DisposeCancelsInflight()
        {
            var p = new Preloader();
            var registry = new StagePreloaderRegistry();
            registry.Register(p);
            registry.StartPreload(5);
            registry.Dispose();
            await UniTask.Yield();
            Assert.That(p.Cancelled, Is.True);
        }
    }
}
