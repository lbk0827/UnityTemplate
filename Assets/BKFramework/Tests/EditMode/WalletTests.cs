using System;
using System.Collections.Generic;
using System.IO;
using BK.Meta;
using BK.Save;
using NUnit.Framework;
using R3;

namespace BK.Tests
{
    public sealed class WalletTests
    {
        private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private string _dir;

        private sealed class Catalog : ICurrencyCatalog
        {
            public IReadOnlyList<CurrencyDefinition> Definitions { get; } = new[]
            {
                new CurrencyDefinition { Id = "Gold", Kind = CurrencyKind.Plain, InitialValue = 100 },
                new CurrencyDefinition
                {
                    Id = "Heart", Kind = CurrencyKind.Rechargeable, InitialValue = 5, RechargeMax = 5, RechargeAmount = 1,
                    RechargeInterval = TimeSpan.FromMinutes(30), InfiniteBuffIds = new[] { "InfiniteHeart" },
                },
                new CurrencyDefinition { Id = "InfiniteHeart", Kind = CurrencyKind.Buff },
            };
        }

        [SetUp] public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private Wallet Make(FakeClock clock, SaveService saves = null) => new(new Catalog(), saves ?? new SaveService(_dir), clock);

        [Test]
        public void NewWalletSeedsInitialValuesAndPersists()
        {
            var saves = new SaveService(_dir);
            using var wallet = Make(new FakeClock(T0), saves);
            Assert.That(wallet.ValueOf("Gold"), Is.EqualTo(100));
            Assert.That(wallet.ValueOf("Heart"), Is.EqualTo(5));
            Assert.That(wallet.ValueOf("InfiniteHeart"), Is.Zero);
            Assert.That(saves.HasUnsavedChanges, Is.True);
            saves.Flush();
            using var again = Make(new FakeClock(T0));
            Assert.That(again.ValueOf("Gold"), Is.EqualTo(100));
        }

        [Test]
        public void SpendAndRefundHeartFollowAnchorRules()
        {
            var clock = new FakeClock(T0);
            using var wallet = Make(clock);
            wallet.Add("Heart", -1, "entry");
            Assert.That(wallet.ValueOf("Heart"), Is.EqualTo(4));
            Assert.That(wallet.Get("Heart").Anchor, Is.EqualTo(T0));
            clock.Advance(TimeSpan.FromMinutes(10));
            wallet.Add("Heart", -1, "entry");
            Assert.That(wallet.Get("Heart").Anchor, Is.EqualTo(T0), "spending below max keeps the running cycle");
            wallet.Add("Heart", +2, "refund");
            Assert.That(wallet.ValueOf("Heart"), Is.EqualTo(5));
            Assert.That(wallet.Get("Heart").Anchor, Is.Null);
        }

        [Test]
        public void OfflineCatchUpOnConstruct()
        {
            var saves = new SaveService(_dir);
            var clock = new FakeClock(T0);
            using (var wallet = Make(clock, saves))
            {
                wallet.Set("Heart", 2, "test");
                saves.Flush();
            }
            using var later = Make(new FakeClock(T0 + TimeSpan.FromMinutes(75)), new SaveService(_dir));
            Assert.That(later.ValueOf("Heart"), Is.EqualTo(4));
            Assert.That(later.TimeToNext("Heart"), Is.EqualTo(TimeSpan.FromMinutes(15)));
        }

        [Test]
        public void TickRechargesOverTime()
        {
            var clock = new FakeClock(T0);
            using var wallet = Make(clock);
            wallet.Set("Heart", 0, "test");
            clock.Advance(TimeSpan.FromMinutes(61));
            wallet.Tick(clock.UtcNow);
            Assert.That(wallet.ValueOf("Heart"), Is.EqualTo(2));
            clock.Advance(TimeSpan.FromHours(10));
            wallet.Tick(clock.UtcNow);
            Assert.That(wallet.ValueOf("Heart"), Is.EqualTo(5));
            Assert.That(wallet.TimeToNext("Heart"), Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void BuffSecondsSetEndTimeAndExpireOnTick()
        {
            var clock = new FakeClock(T0);
            using var wallet = Make(clock);
            wallet.Set("InfiniteHeart", 60, "purchase");
            Assert.That(wallet.IsInfinite("Heart"), Is.True);
            Assert.That(wallet.Get("Heart").InfiniteEnabled.CurrentValue, Is.True);
            wallet.Add("InfiniteHeart", 30, "bonus");
            Assert.That(wallet.Get("InfiniteHeart").BuffEnd, Is.EqualTo(T0 + TimeSpan.FromSeconds(90)));
            clock.Advance(TimeSpan.FromSeconds(91));
            wallet.Tick(clock.UtcNow);
            Assert.That(wallet.IsInfinite("Heart"), Is.False);
            Assert.That(wallet.ValueOf("InfiniteHeart"), Is.Zero);
            Assert.That(wallet.Get("InfiniteHeart").BuffEnd, Is.Null);
        }

        [Test]
        public void AddBelowZeroClampsToZeroAndChangedEmitsDeltaAndReason()
        {
            using var wallet = Make(new FakeClock(T0));
            var changes = new List<WalletChange>();
            using var subscription = wallet.Changed.Subscribe(changes.Add);
            wallet.Add("Gold", -500, "overspend");
            Assert.That(wallet.ValueOf("Gold"), Is.Zero);
            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].CurrencyId, Is.EqualTo("Gold"));
            Assert.That(changes[0].Delta, Is.EqualTo(-100));
            Assert.That(changes[0].Reason, Is.EqualTo("overspend"));
        }

        [Test]
        public void UnknownCurrencyThrows()
        {
            using var wallet = Make(new FakeClock(T0));
            Assert.Throws<KeyNotFoundException>(() => wallet.ValueOf("Gem"));
        }
    }
}
