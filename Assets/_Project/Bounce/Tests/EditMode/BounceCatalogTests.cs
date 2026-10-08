using System.Linq;
using BK.Kit;
using BK.Meta;
using NUnit.Framework;

public sealed class BounceCatalogTests
{
    [Test]
    public void StepOffersAreLinearWithFreeFirstStepsAndPricedPaidSteps()
    {
        var catalog = new BounceStepOffers();
        Assert.That(catalog.UnlockStage, Is.EqualTo(2));
        Assert.That(catalog.Offers.Count, Is.EqualTo(2));
        foreach (var offer in catalog.Offers)
        {
            Assert.That(offer.MaxStep, Is.GreaterThan(0));
            for (int i = 0; i < offer.Steps.Length; i++)
            {
                var step = offer.Steps[i];
                Assert.That(step.Step, Is.EqualTo(i + 1), offer.OfferId + " steps are 1-based and contiguous");
                if (step.IsPaid)
                {
                    Assert.That(int.TryParse(step.ProductId, out var product) && product >= 1001, Is.True, "paid steps link an endless_* shop product");
                    Assert.That(step.Rewards, Is.Empty, "the store grants paid contents");
                }
                else
                {
                    Assert.That(step.Rewards.Length, Is.GreaterThan(0));
                    foreach (var reward in step.Rewards)
                        Assert.That(BounceItems.IsGold(reward) || BounceItems.TryGetBooster(reward, out _), Is.True, reward.itemId);
                }
            }
            Assert.That(offer.Steps[0].IsPaid, Is.False, "first step must be collectable offline");
            Assert.That(offer.Steps[1].IsPaid, Is.True, "second step shows the paid path");
        }
        Assert.That(StepOfferLogic.Select(catalog.Offers, StepOfferType.Vertical).OfferId, Is.EqualTo(BounceStepOffers.VerticalOfferId));
        Assert.That(StepOfferLogic.Select(catalog.Offers, StepOfferType.Chain).OfferId, Is.EqualTo(BounceStepOffers.ChainOfferId));
        Assert.That(StepOfferLogic.Select(catalog.Offers, StepOfferType.Vertical).MaxStep, Is.EqualTo(4), "imported vertical popup has 4 slots");
        Assert.That(StepOfferLogic.Select(catalog.Offers, StepOfferType.Chain).MaxStep, Is.EqualTo(7), "imported chain popup has 7 slots");
        Assert.That(catalog.EpochUtc.DayOfWeek, Is.EqualTo(System.DayOfWeek.Monday));
    }

    [Test]
    public void DailyCatalogFollowsTheTemplateShape()
    {
        var catalog = new BounceDailyRewards();
        Assert.That(catalog.Days.Count, Is.EqualTo(7));
        Assert.That(catalog.BonusSlots.Count, Is.EqualTo(3));
        Assert.That(catalog.Hourly.Length, Is.EqualTo(1));
        foreach (var grant in catalog.Days.Concat(catalog.BonusSlots).SelectMany(d => d).Concat(catalog.Hourly))
        {
            Assert.That(grant.amount, Is.GreaterThan(0));
            Assert.That(BounceItems.IsGold(grant) || BounceItems.TryGetBooster(grant, out _), Is.True, grant.itemId);
        }
    }

    [Test]
    public void WinStreakTiersAscendBelowTheCap()
    {
        var catalog = new BounceWinStreak();
        Assert.That(catalog.Cap, Is.EqualTo(10));
        var thresholds = catalog.Tiers.Select(t => t.Threshold).ToList();
        Assert.That(thresholds, Is.Ordered.Ascending.And.Unique);
        Assert.That(thresholds.Last(), Is.LessThanOrEqualTo(catalog.Cap));
        Assert.That(WinStreakLogic.RewardFor(3, catalog.Tiers).Rewards[0].itemId, Is.EqualTo(BoosterKind.Missile.ToString()));
        Assert.That(WinStreakLogic.RewardFor(2, catalog.Tiers), Is.Null);
    }

    [Test]
    public void DescribeListsGoldThenBoosters()
    {
        Assert.That(BounceItems.Describe(new[] { BounceItems.Gold(200), BounceItems.Booster(BoosterKind.Missile, 1) }), Is.EqualTo("200 Gold, Missile x1"));
        Assert.That(BounceItems.Describe(new ItemGrant[0]), Is.Empty);
    }
}
