using System;
using System.IO;
using System.Linq;
using BK.Kit;
using NUnit.Framework;

public sealed class OfferTests
{
    private string directory;
    [SetUp] public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "BKKitTests", Guid.NewGuid().ToString("N"));
    [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [Test]
    public void CatalogStepsAreOrderedAndComplete()
    {
        Assert.That(OfferCatalog.EndlessOffer.Count, Is.EqualTo(4));
        Assert.That(OfferCatalog.EndlessGift.Count, Is.EqualTo(7));
        foreach (var kind in new[] { OfferKind.EndlessOffer, OfferKind.EndlessGift })
        {
            var steps = OfferCatalog.Steps(kind);
            for (int i = 0; i < steps.Count; i++)
            {
                Assert.That(steps[i].Step, Is.EqualTo(i));
                Assert.That(steps[i].Rewards.Count, Is.GreaterThan(0));
                Assert.That(string.IsNullOrEmpty(steps[i].Price), Is.EqualTo(!steps[i].Paid));
                foreach (var reward in steps[i].Rewards)
                    Assert.That(reward.IsGold || reward.TryGetBooster(out _), Is.True, reward.ItemId);
            }
            Assert.That(steps[0].Paid, Is.False, "first step must be collectable offline");
        }
        Assert.That(OfferCatalog.Steps(OfferKind.WelcomeDeal), Is.Empty);
        Assert.That(OfferCatalog.WelcomeDeal.Rewards.Any(r => r.IsGold), Is.True);
    }

    [Test]
    public void FreeStepGrantsRewardsAndAdvances()
    {
        var progress = new PlayerProgress();
        Assert.That(OfferClaim.Apply(progress, OfferKind.EndlessOffer, 0, out var message), Is.True, message);
        Assert.That(progress.gold, Is.EqualTo(100));
        Assert.That(progress.Step(OfferKind.EndlessOffer), Is.EqualTo(1));
        Assert.That(OfferClaim.Apply(progress, OfferKind.EndlessOffer, 0, out message), Is.False, "same step cannot be claimed twice");
        Assert.That(OfferClaim.Apply(progress, OfferKind.EndlessOffer, 1, out message), Is.False, "paid step is rejected offline");
        Assert.That(progress.gold, Is.EqualTo(100));
        Assert.That(progress.Step(OfferKind.EndlessOffer), Is.EqualTo(1));
        progress.SetStep(OfferKind.EndlessOffer, 2);
        Assert.That(OfferClaim.Apply(progress, OfferKind.EndlessOffer, 2, out message), Is.True, message);
        Assert.That(progress.Count(BoosterKind.Bomb), Is.EqualTo(1));
        progress.SetStep(OfferKind.EndlessOffer, 4);
        Assert.That(OfferClaim.CanClaim(progress, OfferKind.EndlessOffer, 4, out _, out message), Is.False);
        Assert.That(message, Does.Contain("collected"));
        Assert.That(OfferClaim.Apply(progress, OfferKind.WelcomeDeal, 0, out _), Is.False);
    }

    [Test]
    public void GoldRewardSaturatesAtMax()
    {
        var progress = new PlayerProgress { gold = int.MaxValue - 10 };
        Assert.That(OfferClaim.Apply(progress, OfferKind.EndlessOffer, 0, out _), Is.True);
        Assert.That(progress.gold, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void OfferProgressSurvivesReloadAndRejectsNegative()
    {
        var store = new LocalSaveStore(directory);
        var data = store.Load();
        Assert.That(data.Step(OfferKind.EndlessOffer), Is.EqualTo(0));
        data.SetStep(OfferKind.EndlessOffer, 2); data.SetStep(OfferKind.EndlessGift, 5);
        store.Save(data);
        var restored = new LocalSaveStore(directory).Load();
        Assert.That(restored.Step(OfferKind.EndlessOffer), Is.EqualTo(2));
        Assert.That(restored.Step(OfferKind.EndlessGift), Is.EqualTo(5));
        File.WriteAllText(Path.Combine(directory, "progress.json"), "{\"version\":2,\"unlockedLevel\":1,\"gold\":0,\"endlessOfferStep\":-1}");
        File.Delete(Path.Combine(directory, "progress.json.bak"));
        var recovered = new LocalSaveStore(directory).Load();
        Assert.That(recovered.Step(OfferKind.EndlessOffer), Is.EqualTo(0), "negative step falls back to fresh profile");
    }
}
