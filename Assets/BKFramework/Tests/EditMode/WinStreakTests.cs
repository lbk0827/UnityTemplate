using System;
using System.Collections.Generic;
using System.IO;
using BK.Meta;
using BK.Save;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class WinStreakLogicTests
    {
        private static readonly WinStreakTier[] Tiers =
        {
            new() { Threshold = 5, Rewards = new[] { new ItemGrant("ExtraBall", 3) } },
            new() { Threshold = 3, Rewards = new[] { new ItemGrant("ExtraBall", 1) } },
            new() { Threshold = 10, Rewards = new[] { new ItemGrant("ExtraBall", 5) } },
        };

        [Test]
        public void IncrementClampsAtCapAndTreatsNegativeAsZero()
        {
            Assert.That(WinStreakLogic.Increment(10, 10), Is.EqualTo(10));
            Assert.That(WinStreakLogic.Increment(-4, 10), Is.EqualTo(1));
            Assert.That(WinStreakLogic.Increment(14, 0), Is.EqualTo(10), "invalid cap falls back to 10");
            Assert.That(WinStreakLogic.Increment(14, 15), Is.EqualTo(15));
        }

        [Test]
        public void RewardForPicksHighestThresholdRegardlessOfOrder()
        {
            Assert.That(WinStreakLogic.RewardFor(2, Tiers), Is.Null);
            Assert.That(WinStreakLogic.RewardFor(4, Tiers).Threshold, Is.EqualTo(3));
            Assert.That(WinStreakLogic.RewardFor(7, Tiers).Threshold, Is.EqualTo(5));
            Assert.That(WinStreakLogic.RewardFor(99, Tiers).Threshold, Is.EqualTo(10));
        }

        [Test]
        public void GaugeFillLandsOnTierLinesAndInterpolates()
        {
            var thresholds = new[] { 3, 5, 10 };
            Assert.That(WinStreakLogic.GaugeFill(0, thresholds), Is.Zero);
            Assert.That(WinStreakLogic.GaugeFill(3, thresholds), Is.EqualTo(1f / 3f).Within(1e-5f));
            Assert.That(WinStreakLogic.GaugeFill(5, thresholds), Is.EqualTo(2f / 3f).Within(1e-5f));
            Assert.That(WinStreakLogic.GaugeFill(1, thresholds), Is.EqualTo(1f / 9f).Within(1e-5f), "first segment: 1 of 3 wins");
            Assert.That(WinStreakLogic.GaugeFill(10, thresholds), Is.EqualTo(1f));
            Assert.That(WinStreakLogic.GaugeFill(12, thresholds), Is.EqualTo(1f));
        }
    }

    public sealed class WinStreakTests
    {
        private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private string _dir;
        private SaveService _saves;
        private Wallet _wallet;
        private StageProgress _progress;
        private WinStreak _streak;

        private sealed class Currencies : ICurrencyCatalog
        {
            public IReadOnlyList<CurrencyDefinition> Definitions { get; } = new[]
            {
                new CurrencyDefinition { Id = "Heart", Kind = CurrencyKind.Rechargeable, InitialValue = 5, RechargeMax = 5, RechargeInterval = TimeSpan.FromMinutes(30) },
            };
        }

        private sealed class Catalog : IWinStreakCatalog
        {
            public int Cap => 10;
            public int UnlockStage => 5;
            public IReadOnlyList<WinStreakTier> Tiers { get; } = new[]
            {
                new WinStreakTier { Threshold = 3, Rewards = new[] { new ItemGrant("ExtraBall", 1) } },
                new WinStreakTier { Threshold = 5, Rewards = new[] { new ItemGrant("ExtraBall", 3) } },
            };
        }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", Guid.NewGuid().ToString("N"));
            _saves = new SaveService(_dir);
            _wallet = new Wallet(new Currencies(), _saves, new FakeClock(T0));
            _progress = new StageProgress(_saves, _wallet, new StageEntryPolicy("Heart", 0));
            _streak = new WinStreak(new Catalog(), _saves, _progress);
        }

        [TearDown]
        public void TearDown()
        {
            _streak.Dispose(); _progress.Dispose(); _wallet.Dispose();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private void Win(int stage) { _progress.TryStart(stage); _progress.Clear(stage); }

        [Test]
        public void WinsAccumulateAndPendingIsOverwrittenPerTier()
        {
            Win(10); Win(11);
            Assert.That(_streak.Current.CurrentValue, Is.EqualTo(2));
            Assert.That(_streak.Pending, Is.Empty);
            Win(12);
            Assert.That(_streak.Pending.Count, Is.EqualTo(1));
            Assert.That(_streak.Pending[0].amount, Is.EqualTo(1));
            Win(13); Win(14);
            Assert.That(_streak.Current.CurrentValue, Is.EqualTo(5));
            Assert.That(_streak.Pending[0].amount, Is.EqualTo(3), "overwritten, not accumulated");
            Assert.That(_streak.GaugeFill, Is.EqualTo(1f));
            Assert.That(_streak.AttemptInProgress, Is.False);
        }

        [Test]
        public void RetryViaStartedWhileInProgressResets_ContinueThenWinKeeps()
        {
            Win(10); Win(11);
            _progress.TryStart(12);            // fail happens, no Cleared
            Assert.That(_streak.Current.CurrentValue, Is.EqualTo(2), "a fail alone does not reset");
            _progress.TryStart(12);            // retry: Started while in progress
            Assert.That(_streak.Current.CurrentValue, Is.Zero);
            _progress.Clear(12);               // continue-then-win on this attempt counts
            Assert.That(_streak.Current.CurrentValue, Is.EqualTo(1));
        }

        [Test]
        public void AbandonIsIdempotentAndClearsPending()
        {
            Win(10); Win(11); Win(12);
            Assert.That(_streak.Pending, Is.Not.Empty);
            _streak.Abandon();
            Assert.That(_streak.Current.CurrentValue, Is.EqualTo(3), "no attempt in progress: no-op");
            _progress.TryStart(13);
            _streak.Abandon();
            Assert.That(_streak.Current.CurrentValue, Is.Zero);
            Assert.That(_streak.Pending, Is.Empty);
            _streak.Abandon();
            Assert.That(_streak.AttemptInProgress, Is.False);
        }

        [Test]
        public void ForceQuitMarkerResetsOnConstructAndStatePersists()
        {
            Win(10); Win(11);
            _progress.TryStart(12);
            _saves.Flush();
            var saves = new SaveService(_dir);
            using var progress = new StageProgress(saves, _wallet, new StageEntryPolicy("Heart", 0));
            using var reborn = new WinStreak(new Catalog(), saves, progress);
            Assert.That(reborn.Current.CurrentValue, Is.Zero, "marker survived the restart: streak forfeited");
            Assert.That(reborn.AttemptInProgress, Is.False);
        }

        [Test]
        public void ClearBelowUnlockStageResetsAndConsumePendingIsOneShot()
        {
            Win(10); Win(11); Win(12);
            var grants = _streak.ConsumePending();
            Assert.That(grants.Count, Is.EqualTo(1));
            Assert.That(_streak.ConsumePending(), Is.Empty);
            Win(2);
            Assert.That(_streak.Current.CurrentValue, Is.Zero);
        }
    }
}
