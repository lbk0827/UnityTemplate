using System;
using System.Collections.Generic;
using System.IO;
using BK.Meta;
using BK.Save;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class FailContinueLogicTests
    {
        [Test]
        public void ComputePriceLadderCapsAtMax()
        {
            var prices = new List<long>();
            foreach (var used in new[] { 0, 1, 2, 3, 4, 99 })
                prices.Add(FailContinueLogic.ComputePrice(900, 1000, 3900, used));
            Assert.That(prices, Is.EqualTo(new long[] { 900, 1900, 2900, 3900, 3900, 3900 }));
        }

        [Test]
        public void CanAffordBoundary()
        {
            Assert.That(FailContinueLogic.CanAfford(900, 900), Is.True);
            Assert.That(FailContinueLogic.CanAfford(899, 900), Is.False);
        }

        [Test]
        public void MovesWinOverTime()
        {
            Assert.That(FailContinueLogic.ResolveGrant(5, 0), Is.EqualTo((true, 5)));
            Assert.That(FailContinueLogic.ResolveGrant(0, 30), Is.EqualTo((false, 30)));
            Assert.That(FailContinueLogic.ResolveGrant(5, 30), Is.EqualTo((true, 5)));
            Assert.That(FailContinueLogic.ResolveGrant(0, 0), Is.EqualTo((false, 0)));
        }

        [Test]
        public void OnlyTimeOverAndMovesZeroOfferContinue()
        {
            Assert.That(FailContinueLogic.OffersContinue(StageFailReason.TimeOver), Is.True);
            Assert.That(FailContinueLogic.OffersContinue(StageFailReason.MovesZero), Is.True);
            Assert.That(FailContinueLogic.OffersContinue(StageFailReason.HpZero), Is.False);
            Assert.That(FailContinueLogic.OffersContinue(StageFailReason.UserQuit), Is.False);
            Assert.That(FailContinueLogic.OffersContinue(StageFailReason.Unknown), Is.False);
        }
    }

    public sealed class ContinueOffersTests
    {
        private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private string _dir;
        private Wallet _wallet;
        private StageProgress _progress;
        private ContinueOffers _offers;

        private sealed class Currencies : ICurrencyCatalog
        {
            public IReadOnlyList<CurrencyDefinition> Definitions { get; } = new[]
            {
                new CurrencyDefinition { Id = "Gold", InitialValue = 3000 },
                new CurrencyDefinition { Id = "Heart", Kind = CurrencyKind.Rechargeable, InitialValue = 5, RechargeMax = 5, RechargeInterval = TimeSpan.FromMinutes(30) },
            };
        }

        private sealed class OfferCatalog : IContinueOfferCatalog
        {
            public IReadOnlyList<ContinueOfferDefinition> Offers { get; } = new[]
            {
                new ContinueOfferDefinition { Reason = StageFailReason.MovesZero, BasePrice = 900, AddPrice = 1000, MaxPrice = 3900, AddMoves = 5 },
                new ContinueOfferDefinition { Reason = StageFailReason.TimeOver, BasePrice = 900, AddPrice = 1000, MaxPrice = 3900, AddTimeSeconds = 30 },
            };
        }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", Guid.NewGuid().ToString("N"));
            var saves = new SaveService(_dir);
            _wallet = new Wallet(new Currencies(), saves, new FakeClock(T0));
            _progress = new StageProgress(saves, _wallet, new StageEntryPolicy("Heart", 0));
            _offers = new ContinueOffers(new OfferCatalog(), _wallet, _progress);
        }

        [TearDown]
        public void TearDown()
        {
            _offers.Dispose(); _progress.Dispose(); _wallet.Dispose();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void PurchaseClimbsTheLadderAndUnaffordableDoesNotCharge()
        {
            _progress.TryStart(1);
            Assert.That(_offers.TryGetOffer(StageFailReason.MovesZero, out var first), Is.True);
            Assert.That(first.Price, Is.EqualTo(900));
            Assert.That(first.CanAfford, Is.True);
            Assert.That(_offers.TryPurchase(first), Is.True);
            Assert.That(_wallet.ValueOf("Gold"), Is.EqualTo(2100));

            Assert.That(_offers.TryGetOffer(StageFailReason.MovesZero, out var second), Is.True);
            Assert.That(second.Price, Is.EqualTo(1900));
            Assert.That(_offers.TryPurchase(second), Is.True);
            Assert.That(_wallet.ValueOf("Gold"), Is.EqualTo(200));

            Assert.That(_offers.TryGetOffer(StageFailReason.MovesZero, out var third), Is.True);
            Assert.That(third.Price, Is.EqualTo(2900));
            Assert.That(third.CanAfford, Is.False);
            Assert.That(_offers.TryPurchase(third), Is.False);
            Assert.That(_wallet.ValueOf("Gold"), Is.EqualTo(200));
            Assert.That(_offers.UsedCount, Is.EqualTo(2));
        }

        [Test]
        public void NewAttemptResetsTheLadderAndUnknownReasonHasNoOffer()
        {
            _progress.TryStart(1);
            _offers.TryGetOffer(StageFailReason.TimeOver, out var offer);
            _offers.TryPurchase(offer);
            Assert.That(_offers.UsedCount, Is.EqualTo(1));
            _progress.TryStart(1);
            Assert.That(_offers.UsedCount, Is.Zero);
            Assert.That(_offers.TryGetOffer(StageFailReason.HpZero, out _), Is.False);
        }
    }
}
