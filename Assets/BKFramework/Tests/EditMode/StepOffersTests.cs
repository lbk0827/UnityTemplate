using System;
using System.Collections.Generic;
using System.IO;
using BK.Meta;
using BK.Save;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class StepOfferLogicTests
    {
        [Test]
        public void TypeAlternatesChainThenVerticalAndNoneBeforeEpoch()
        {
            Assert.That(StepOfferLogic.TypeOfWeek(-1), Is.EqualTo(StepOfferType.None));
            Assert.That(StepOfferLogic.TypeOfWeek(0), Is.EqualTo(StepOfferType.Chain));
            Assert.That(StepOfferLogic.TypeOfWeek(1), Is.EqualTo(StepOfferType.Vertical));
            Assert.That(StepOfferLogic.TypeOfWeek(2), Is.EqualTo(StepOfferType.Chain));
        }

        [Test]
        public void SelectTakesLowestOfferIdWithStepsAndWindowHasOneCurrent()
        {
            var a = new StepOfferDefinition { OfferId = 5, Type = StepOfferType.Chain, Steps = Steps(3) };
            var b = new StepOfferDefinition { OfferId = 2, Type = StepOfferType.Chain, Steps = Steps(4) };
            var empty = new StepOfferDefinition { OfferId = 1, Type = StepOfferType.Chain };
            Assert.That(StepOfferLogic.Select(new[] { a, b, empty }, StepOfferType.Chain), Is.SameAs(b));
            Assert.That(StepOfferLogic.Select(new[] { a, b }, StepOfferType.Vertical), Is.Null);

            var window = StepOfferLogic.VisibleWindow(b, nextStep: 2, slotCount: 5);
            Assert.That(window.Count, Is.EqualTo(3), "steps 2..4; past steps are not rendered");
            Assert.That(window[0].IsCurrent && window[0].Step.Step == 2, Is.True);
            Assert.That(window[1].IsLocked && window[2].IsLocked, Is.True);
            Assert.That(StepOfferLogic.VisibleWindow(b, 5, 5), Is.Empty);
        }

        private static StepOfferStep[] Steps(int count)
        {
            var steps = new StepOfferStep[count];
            for (var i = 0; i < count; i++) steps[i] = new StepOfferStep { Step = i + 1 };
            return steps;
        }
    }

    public sealed class StepOffersTests
    {
        private static readonly DateTime Epoch = new(2026, 1, 2, 7, 0, 0, DateTimeKind.Utc);
        private string _dir;
        private FakeClock _clock;
        private SaveService _saves;
        private Wallet _wallet;
        private StageProgress _progress;
        private StepOffers _offers;

        private sealed class Currencies : ICurrencyCatalog
        {
            public IReadOnlyList<CurrencyDefinition> Definitions { get; } = new[]
            {
                new CurrencyDefinition { Id = "Gold" },
                new CurrencyDefinition { Id = "Missile" },
                new CurrencyDefinition { Id = "Heart", Kind = CurrencyKind.Rechargeable, InitialValue = 5, RechargeMax = 5, RechargeInterval = TimeSpan.FromMinutes(30) },
            };
        }

        private sealed class Catalog : IStepOfferCatalog
        {
            public DateTime EpochUtc => Epoch;
            public int UnlockStage => 41;
            public IReadOnlyList<StepOfferDefinition> Offers { get; } = new[]
            {
                new StepOfferDefinition
                {
                    OfferId = 1, Type = StepOfferType.Vertical, Steps = new[]
                    {
                        new StepOfferStep { Step = 1, Rewards = new[] { new ItemGrant("Gold", 100) } },
                        new StepOfferStep { Step = 2, Rewards = new[] { new ItemGrant("Missile", 1) } },
                        new StepOfferStep { Step = 3, IsPaid = true, ProductId = "offer_1001" },
                        new StepOfferStep { Step = 4, Rewards = new[] { new ItemGrant("Gold", 300) } },
                    },
                },
                new StepOfferDefinition
                {
                    OfferId = 2, Type = StepOfferType.Chain, Steps = new[]
                    {
                        new StepOfferStep { Step = 1, Rewards = new[] { new ItemGrant("Gold", 50) } },
                        new StepOfferStep { Step = 2, IsPaid = true, ProductId = "offer_1011" },
                    },
                },
            };
        }

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", Guid.NewGuid().ToString("N"));
            _clock = new FakeClock(Epoch + TimeSpan.FromDays(7) + TimeSpan.FromHours(1)); // week 1 = Vertical
            _saves = new SaveService(_dir);
            _wallet = new Wallet(new Currencies(), _saves, _clock);
            _progress = new StageProgress(_saves, _wallet, new StageEntryPolicy("Heart", 0));
            _progress.Clear(49); // current stage 50
            _offers = new StepOffers(new Catalog(), _saves, _wallet, _clock, _progress);
        }

        [TearDown]
        public void TearDown()
        {
            _offers.Dispose(); _progress.Dispose(); _wallet.Dispose();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void OddWeekIsVerticalEvenWeekIsChain_RoundStampsWeekStart()
        {
            var c = _offers.GetActive();
            Assert.That(c.HasValue && c.Value.Definition.Type == StepOfferType.Vertical, Is.True);
            Assert.That(c.Value.WeekIndex, Is.EqualTo(1));
            Assert.That(c.Value.EndUtc, Is.EqualTo(Epoch + TimeSpan.FromDays(14)));
            Assert.That(c.Value.Remaining(_clock.UtcNow), Is.EqualTo(TimeSpan.FromDays(7) - TimeSpan.FromHours(1)));

            _clock.Advance(TimeSpan.FromDays(7));
            var next = _offers.GetActive();
            Assert.That(next.Value.Definition.Type, Is.EqualTo(StepOfferType.Chain));
            Assert.That(next.Value.NextStep, Is.EqualTo(1));
        }

        [Test]
        public void FreeClaimAdvancesAndGrants_PaidRefused_ProgressSurvivesReentry()
        {
            var c = _offers.GetActive().Value;
            Assert.That(_offers.TryClaimFree(c), Is.True);
            Assert.That(_wallet.ValueOf("Gold"), Is.EqualTo(100));
            Assert.That(_offers.TryClaimFree(c), Is.False, "stale card for step 1 cannot claim twice");

            c = _offers.GetActive().Value;
            Assert.That(c.NextStep, Is.EqualTo(2));
            Assert.That(_offers.TryClaimFree(c), Is.True);
            Assert.That(_wallet.ValueOf("Missile"), Is.EqualTo(1));

            c = _offers.GetActive().Value;
            Assert.That(c.Current.IsPaid, Is.True);
            Assert.That(_offers.TryClaimFree(c), Is.False);
            Assert.That(_offers.ConfirmPaid(c, "wrong_product"), Is.False);
            Assert.That(_offers.ConfirmPaid(c, "offer_1001"), Is.True);
            Assert.That(_wallet.ValueOf("Gold"), Is.EqualTo(100), "paid step grants nothing here");

            _saves.Flush();
            using var reloaded = new StepOffers(new Catalog(), new SaveService(_dir), _wallet, _clock, _progress);
            Assert.That(reloaded.GetActive().Value.NextStep, Is.EqualTo(4));
        }

        [Test]
        public void NewWeekResetsRoundAndOldCampaignIsRejected()
        {
            var old = _offers.GetActive().Value;
            _offers.TryClaimFree(old);
            _clock.Advance(TimeSpan.FromDays(14)); // week 3: Vertical again, new round
            var fresh = _offers.GetActive().Value;
            Assert.That(fresh.WeekIndex, Is.EqualTo(3));
            Assert.That(fresh.NextStep, Is.EqualTo(1));
            Assert.That(_offers.TryClaimFree(old), Is.False);
            Assert.That(_wallet.ValueOf("Gold"), Is.EqualTo(100));
        }

        [Test]
        public void CompletedExpiredLockedAndPreEpochAreInactive()
        {
            var c = _offers.GetActive().Value;
            _offers.TryClaimFree(c); c = _offers.GetActive().Value;
            _offers.TryClaimFree(c); c = _offers.GetActive().Value;
            _offers.ConfirmPaid(c, "offer_1001"); c = _offers.GetActive().Value;
            Assert.That(_offers.TryClaimFree(c), Is.True);
            Assert.That(_offers.GetActive(), Is.Null, "all four steps done");

            _clock.UtcNow = Epoch + TimeSpan.FromDays(14) - TimeSpan.FromSeconds(1);
            Assert.That(_offers.GetActive(), Is.Null, "still the completed round until the week ends");

            _clock.UtcNow = Epoch - TimeSpan.FromDays(1);
            Assert.That(_offers.GetActive(), Is.Null, "before the epoch");

            _clock.UtcNow = Epoch + TimeSpan.FromDays(7);
            using var locked = new StepOffers(new Catalog(), _saves, _wallet, _clock,
                new StageProgress(new SaveService(Path.Combine(_dir, "other")), _wallet, new StageEntryPolicy("Heart", 0)));
            Assert.That(locked.IsUnlocked, Is.False);
            Assert.That(locked.GetActive(), Is.Null);
        }

        [Test]
        public void AutoOpenedIsPerRound()
        {
            var c = _offers.GetActive().Value;
            Assert.That(c.AutoOpened, Is.False);
            _offers.MarkAutoOpened(c);
            Assert.That(_offers.GetActive().Value.AutoOpened, Is.True);
            _clock.Advance(TimeSpan.FromDays(14));
            Assert.That(_offers.GetActive().Value.AutoOpened, Is.False);
        }
    }
}
