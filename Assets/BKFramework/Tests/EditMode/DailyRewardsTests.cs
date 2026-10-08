using System;
using System.Collections.Generic;
using System.IO;
using BK.Meta;
using BK.Save;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class DailyRewardsTests
    {
        private string _dir;
        private FakeClock _clock;
        private SaveService _saves;
        private Wallet _wallet;
        private DailyRewards _daily;

        private sealed class Currencies : ICurrencyCatalog
        {
            public IReadOnlyList<CurrencyDefinition> Definitions { get; } = new[]
            {
                new CurrencyDefinition { Id = "Gold" },
                new CurrencyDefinition { Id = "InfiniteHeart", Kind = CurrencyKind.Buff },
            };
        }

        private sealed class Catalog : IDailyRewardCatalog
        {
            public IReadOnlyList<ItemGrant[]> Days { get; } = new[]
            {
                new[] { new ItemGrant("Gold", 10) }, new[] { new ItemGrant("Gold", 20) }, new[] { new ItemGrant("Gold", 30) },
            };
            public IReadOnlyList<ItemGrant[]> BonusSlots { get; } = new[]
            {
                new[] { new ItemGrant("Gold", 50) }, new[] { new ItemGrant("InfiniteHeart", 300) }, new[] { new ItemGrant("Gold", 100) },
            };
            public ItemGrant[] Hourly { get; } = { new ItemGrant("Gold", 5) };
        }

        /// <summary>A UTC instant that is 23:30 local time, so one hour later is a new local day.</summary>
        private static DateTime LateEvening()
        {
            var local = new DateTime(2026, 3, 10, 23, 30, 0, DateTimeKind.Local);
            return local.ToUniversalTime();
        }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", Guid.NewGuid().ToString("N"));
            _clock = new FakeClock(LateEvening());
            _saves = new SaveService(_dir);
            _wallet = new Wallet(new Currencies(), _saves, _clock);
            _daily = new DailyRewards(new Catalog(), _saves, _wallet, _clock);
        }

        [TearDown]
        public void TearDown()
        {
            _daily.Dispose(); _wallet.Dispose();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void FirstClaimIsDayZero_ThenLockedUntilNextLocalDate_AndWrapsAfterCycle()
        {
            Assert.That(_daily.CanClaimDaily, Is.True);
            Assert.That(_daily.TryClaimDaily(out var granted), Is.True);
            Assert.That(granted[0].amount, Is.EqualTo(10));
            Assert.That(_daily.NextDay, Is.EqualTo(1));
            Assert.That(_daily.CanClaimDaily, Is.False);
            Assert.That(_daily.TryClaimDaily(out _), Is.False);

            _clock.Advance(TimeSpan.FromHours(1)); // crosses local midnight
            Assert.That(_daily.CanClaimDaily, Is.True);
            _daily.TryClaimDaily(out _);
            _clock.Advance(TimeSpan.FromDays(5)); // missed days: no penalty, no catch-up
            Assert.That(_daily.TryClaimDaily(out granted), Is.True);
            Assert.That(granted[0].amount, Is.EqualTo(30));
            Assert.That(_daily.NextDay, Is.Zero, "wraps after the cycle");
            Assert.That(_wallet.ValueOf("Gold"), Is.EqualTo(60));
        }

        [Test]
        public void BonusSlotsAreSequentialAndResetOnNewLocalDate()
        {
            Assert.That(_daily.CanClaimBonus(1), Is.False);
            Assert.That(_daily.TryClaimBonus(0, out _), Is.True);
            Assert.That(_daily.TryClaimBonus(0, out _), Is.False);
            Assert.That(_daily.TryClaimBonus(1, out var buff), Is.True);
            Assert.That(buff[0].itemId, Is.EqualTo("InfiniteHeart"));
            Assert.That(_daily.BonusClaimed, Is.EqualTo(2));
            _clock.Advance(TimeSpan.FromHours(1));
            Assert.That(_daily.BonusClaimed, Is.Zero);
            Assert.That(_daily.CanClaimBonus(0), Is.True);
        }

        [Test]
        public void HourlyGiftNeedsOneHourAndBadgeCountsEverything()
        {
            Assert.That(_daily.ClaimableCount, Is.EqualTo(1 + 3 + 1));
            Assert.That(_daily.TryClaimHourly(out _), Is.True);
            Assert.That(_daily.CanClaimHourly, Is.False);
            Assert.That(_daily.TimeToHourly, Is.EqualTo(TimeSpan.FromHours(1)));
            _clock.Advance(TimeSpan.FromMinutes(59));
            Assert.That(_daily.CanClaimHourly, Is.False);
            _clock.Advance(TimeSpan.FromMinutes(1));
            Assert.That(_daily.CanClaimHourly, Is.True);
            Assert.That(_daily.ClaimableCount, Is.EqualTo(1 + 3 + 1), "new local day restored the daily and bonus slots");
        }

        [Test]
        public void StatePersists()
        {
            _daily.TryClaimDaily(out _);
            _daily.TryClaimBonus(0, out _);
            _saves.Flush();
            using var reloaded = new DailyRewards(new Catalog(), new SaveService(_dir), _wallet, _clock);
            Assert.That(reloaded.NextDay, Is.EqualTo(1));
            Assert.That(reloaded.CanClaimDaily, Is.False);
            Assert.That(reloaded.BonusClaimed, Is.EqualTo(1));
        }
    }
}
