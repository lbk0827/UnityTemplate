using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BK.Notifications;
using BK.Save;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class LocalPushServiceTests
    {
        private static readonly DateTime T0 = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);
        private string _dir;

        private sealed class Platform : INotificationPlatform
        {
            public bool Supported = true, Answer = true;
            public int Requests, Cancels;
            public readonly List<NotificationRequest> Scheduled = new();
            public bool IsSupported => Supported;
            public UniTask<bool> RequestPermissionAsync() { Requests++; return UniTask.FromResult(Answer); }
            public void Schedule(NotificationRequest request) => Scheduled.Add(request);
            public void CancelAll() { Cancels++; Scheduled.Clear(); }
        }

        private sealed class Provider : IPushProvider
        {
            public Func<DateTime, IEnumerable<NotificationRequest>> Builder;
            public IEnumerable<NotificationRequest> Build(DateTime nowLocal) => Builder(nowLocal);
        }

        [SetUp] public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private static Provider Soon(int seconds, int id = 1)
            => new() { Builder = now => new[] { new NotificationRequest(id, "t", "b", now.AddSeconds(seconds)) } };

        [Test]
        public async Task PermissionFlowIsPendingThenRequestedOnce()
        {
            var saves = new SaveService(_dir, 0f);
            var platform = new Platform();
            using var push = new LocalPushService(saves, new FakeClock(T0), platform, Array.Empty<IPushProvider>());
            Assert.That(await push.TryRequestPendingPermissionAsync(), Is.False, "nothing pending: no prompt");
            Assert.That(platform.Requests, Is.Zero);

            push.MarkPendingPermissionRequest();
            Assert.That(push.IsPermissionPending, Is.True);
            Assert.That(await push.TryRequestPendingPermissionAsync(), Is.True);
            Assert.That(platform.Requests, Is.EqualTo(1));
            Assert.That(push.PermissionRequested && push.PermissionGranted, Is.True);

            push.MarkPendingPermissionRequest();
            Assert.That(push.IsPermissionPending, Is.False, "never asked twice");
            saves.Flush();
            using var reloaded = new LocalPushService(new SaveService(_dir, 0f), new FakeClock(T0), platform, Array.Empty<IPushProvider>());
            Assert.That(reloaded.PermissionGranted, Is.True);
        }

        [Test]
        public async Task DeniedPermissionIsRecordedAndNothingIsScheduled()
        {
            var platform = new Platform { Answer = false };
            using var push = new LocalPushService(new SaveService(_dir, 0f), new FakeClock(T0), platform, new[] { Soon(60) });
            push.MarkPendingPermissionRequest();
            Assert.That(await push.TryRequestPendingPermissionAsync(), Is.False);
            Assert.That(push.PermissionRequested, Is.True);
            Assert.That(push.OnBackgrounded(), Is.Zero);
            Assert.That(platform.Cancels, Is.EqualTo(1), "the OS queue is still cleared");
            Assert.That(platform.Scheduled, Is.Empty);
        }

        [Test]
        public async Task BackgroundSchedulesFutureRequestsAndForegroundCancels()
        {
            var platform = new Platform();
            using var push = new LocalPushService(new SaveService(_dir, 0f), new FakeClock(T0), platform,
                new[] { Soon(60, 1), Soon(0, 2), Soon(-30, 3), Soon(3600, 4) });
            push.MarkPendingPermissionRequest();
            await push.TryRequestPendingPermissionAsync();
            Assert.That(push.OnBackgrounded(), Is.EqualTo(2), "immediate and past requests are skipped");
            Assert.That(platform.Scheduled.Select(r => r.Id), Is.EqualTo(new[] { 1, 4 }));
            Assert.That(push.LastScheduled.Count, Is.EqualTo(2));
            push.OnForegrounded();
            Assert.That(platform.Scheduled, Is.Empty);
            Assert.That(platform.Cancels, Is.EqualTo(2));
            Assert.That(push.LastScheduled, Is.Empty);
        }

        [Test]
        public async Task FailingProviderDoesNotBlockOthers()
        {
            var platform = new Platform();
            var broken = new Provider { Builder = _ => throw new InvalidOperationException("boom") };
            using var push = new LocalPushService(new SaveService(_dir, 0f), new FakeClock(T0), platform, new IPushProvider[] { broken, Soon(60, 9) });
            push.MarkPendingPermissionRequest();
            await push.TryRequestPendingPermissionAsync();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("Provider failed"));
            Assert.That(push.OnBackgrounded(), Is.EqualTo(1));
            Assert.That(platform.Scheduled[0].Id, Is.EqualTo(9));
        }
    }

    public sealed class DailyRetentionPushProviderTests
    {
        private static DailyRetentionConfig Config(int days = 30, int bodies = 5)
            => new() { Days = days, Hour = 18, Minute = 40, Title = "Come back", Bodies = Enumerable.Range(0, bodies).Select(i => "m" + i).ToArray() };

        [Test]
        public void FansOutOneSlotPerDayFromTodayOrTomorrow()
        {
            var provider = new DailyRetentionPushProvider(Config(), _ => 1);
            var morning = new DateTime(2026, 3, 10, 9, 0, 0);
            var plan = provider.Build(morning).ToList();
            Assert.That(plan.Count, Is.EqualTo(30));
            Assert.That(plan.Select(r => r.Id), Is.EqualTo(Enumerable.Range(100200, 30)));
            Assert.That(plan[0].FireAtLocal, Is.EqualTo(new DateTime(2026, 3, 10, 18, 40, 0)));
            Assert.That(plan[29].FireAtLocal, Is.EqualTo(new DateTime(2026, 4, 8, 18, 40, 0)));

            var evening = new DateTime(2026, 3, 10, 19, 0, 0);
            Assert.That(provider.Build(evening).First().FireAtLocal, Is.EqualTo(new DateTime(2026, 3, 11, 18, 40, 0)), "time passed: start tomorrow");
        }

        [Test]
        public void BodiesCycleAShuffledPoolAndTheSeedIsStable()
        {
            var provider = new DailyRetentionPushProvider(Config(days: 10, bodies: 5), _ => 42);
            var now = new DateTime(2026, 3, 10, 9, 0, 0);
            var bodies = provider.Build(now).Select(r => r.Body).ToList();
            Assert.That(bodies.Take(5), Is.EquivalentTo(new[] { "m0", "m1", "m2", "m3", "m4" }), "the first cycle uses every message once");
            Assert.That(bodies.Skip(5), Is.EqualTo(bodies.Take(5)), "then the same order repeats");
            Assert.That(provider.Build(now).Select(r => r.Body), Is.EqualTo(bodies), "same seed, same plan");
            Assert.That(new DailyRetentionPushProvider(Config(days: 10, bodies: 5), _ => 7).Build(now).Select(r => r.Body), Is.Not.EqualTo(bodies));
            Assert.That(new DailyRetentionPushProvider(Config(bodies: 0)).Build(now), Is.Empty);
        }
    }
}
