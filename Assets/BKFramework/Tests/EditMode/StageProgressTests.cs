using System;
using System.Collections.Generic;
using System.IO;
using BK.Meta;
using BK.Save;
using NUnit.Framework;
using R3;

namespace BK.Tests
{
    public sealed class StageProgressTests
    {
        private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private string _dir;
        private FakeClock _clock;
        private SaveService _saves;
        private Wallet _wallet;
        private StageProgress _progress;

        private sealed class Catalog : ICurrencyCatalog
        {
            public IReadOnlyList<CurrencyDefinition> Definitions { get; } = new[]
            {
                new CurrencyDefinition
                {
                    Id = "Heart", Kind = CurrencyKind.Rechargeable, InitialValue = 5, RechargeMax = 5, RechargeAmount = 1,
                    RechargeInterval = TimeSpan.FromMinutes(30), InfiniteBuffIds = new[] { "InfiniteHeart" },
                },
                new CurrencyDefinition { Id = "InfiniteHeart", Kind = CurrencyKind.Buff },
            };
        }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", Guid.NewGuid().ToString("N"));
            _clock = new FakeClock(T0);
            _saves = new SaveService(_dir);
            _wallet = new Wallet(new Catalog(), _saves, _clock);
            _progress = new StageProgress(_saves, _wallet, new StageEntryPolicy("Heart", freeUntilStage: 4));
        }

        [TearDown]
        public void TearDown()
        {
            _progress.Dispose(); _wallet.Dispose();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private long Hearts => _wallet.ValueOf("Heart");

        [Test]
        public void FreeStagesNeverChargeOrRefund()
        {
            Assert.That(_progress.TryStart(3), Is.True);
            Assert.That(Hearts, Is.EqualTo(5));
            _progress.Clear(3);
            Assert.That(Hearts, Is.EqualTo(5));
            Assert.That(_progress.CurrentStage.CurrentValue, Is.EqualTo(4));
        }

        [Test]
        public void StartChargesOnceAndClearRefunds()
        {
            Assert.That(_progress.TryStart(5), Is.True);
            Assert.That(Hearts, Is.EqualTo(4));
            _progress.Clear(5);
            Assert.That(Hearts, Is.EqualTo(5));
            Assert.That(_progress.CurrentStage.CurrentValue, Is.EqualTo(6));
            Assert.That(_progress.AttemptsOnCurrentStage, Is.Zero);
        }

        [Test]
        public void RetryChargesAgainAndCountsAttempts_FailDoesNot()
        {
            var attempts = new List<int>();
            using var sub = _progress.Started.Subscribe(s => attempts.Add(s.Attempt));
            Assert.That(_progress.TryStart(5), Is.True);
            Assert.That(_progress.TryStart(5), Is.True);
            Assert.That(Hearts, Is.EqualTo(3));
            Assert.That(attempts, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(_progress.AttemptsOnCurrentStage, Is.EqualTo(2));
        }

        [Test]
        public void StartFailsAtZeroHeartsWithoutSideEffects()
        {
            _wallet.Set("Heart", 0, "test");
            var started = 0;
            using var sub = _progress.Started.Subscribe(_ => started++);
            Assert.That(_progress.CanEnter(5), Is.False);
            Assert.That(_progress.TryStart(5), Is.False);
            Assert.That(started, Is.Zero);
            Assert.That(_progress.AttemptsOnCurrentStage, Is.Zero);
            Assert.That(_progress.CanEnter(2), Is.True, "free stages are enterable with no hearts");
        }

        [Test]
        public void InfiniteBuffSkipsChargeAndRefund()
        {
            _wallet.Set("Heart", 2, "test");
            _wallet.Set("InfiniteHeart", 600, "purchase");
            Assert.That(_progress.TryStart(7), Is.True);
            Assert.That(Hearts, Is.EqualTo(2));
            _progress.Clear(7);
            Assert.That(Hearts, Is.EqualTo(2));
        }

        [Test]
        public void RefundIsSkippedWhenAlreadyFull()
        {
            Assert.That(_progress.TryStart(5), Is.True);
            _wallet.Set("Heart", 5, "refill");
            _progress.Clear(5);
            Assert.That(Hearts, Is.EqualTo(5), "never refund above the recharge max");
        }

        [Test]
        public void ClearSavesPointerBeforeEventsAndNeverMovesBackwards()
        {
            int seen = -1;
            using var sub = _progress.Cleared.Subscribe(_ => seen = _progress.CurrentStage.CurrentValue);
            _progress.TryStart(5); _progress.Clear(5);
            Assert.That(seen, Is.EqualTo(6));
            _progress.TryStart(2); _progress.Clear(2);
            Assert.That(_progress.CurrentStage.CurrentValue, Is.EqualTo(6), "replaying an old stage keeps the pointer");
            _saves.Flush();
            using var reloaded = new StageProgress(new SaveService(_dir), _wallet, new StageEntryPolicy("Heart", 4));
            Assert.That(reloaded.CurrentStage.CurrentValue, Is.EqualTo(6));
        }
    }
}
