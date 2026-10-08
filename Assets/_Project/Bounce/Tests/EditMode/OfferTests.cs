using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BK.Core.Time;
using BK.Kit;
using BK.Meta;
using BK.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
    public void FreeStepGrantsRewardsThroughTheWalletAndAdvances()
    {
        var saves = new SaveService(directory, 0f);
        using var wallet = new Wallet(new BounceCurrencies(), saves, new SystemClock());
        var offers = saves.Get<BounceOfferData>();
        Assert.That(OfferClaim.Apply(offers, wallet, OfferKind.EndlessOffer, 0, out var message), Is.True, message);
        Assert.That(wallet.ValueOf(BounceCurrencies.Gold), Is.EqualTo(100));
        Assert.That(offers.Step(OfferKind.EndlessOffer), Is.EqualTo(1));
        Assert.That(OfferClaim.Apply(offers, wallet, OfferKind.EndlessOffer, 0, out message), Is.False, "same step cannot be claimed twice");
        Assert.That(OfferClaim.Apply(offers, wallet, OfferKind.EndlessOffer, 1, out message), Is.False, "paid step is rejected offline");
        Assert.That(wallet.ValueOf(BounceCurrencies.Gold), Is.EqualTo(100));
        offers.SetStep(OfferKind.EndlessOffer, 2);
        Assert.That(OfferClaim.Apply(offers, wallet, OfferKind.EndlessOffer, 2, out message), Is.True, message);
        Assert.That(wallet.ValueOf(BoosterKind.Bomb.ToString()), Is.EqualTo(1));
        offers.SetStep(OfferKind.EndlessOffer, 4);
        Assert.That(OfferClaim.CanClaim(offers, OfferKind.EndlessOffer, 4, out _, out message), Is.False);
        Assert.That(message, Does.Contain("collected"));
        Assert.That(OfferClaim.Apply(offers, wallet, OfferKind.WelcomeDeal, 0, out _), Is.False);
    }

    [Test]
    public void OfferProgressSurvivesReloadAndRejectsNegative()
    {
        var saves = new SaveService(directory, 0f);
        var data = saves.Get<BounceOfferData>();
        Assert.That(data.Step(OfferKind.EndlessOffer), Is.EqualTo(0));
        data.SetStep(OfferKind.EndlessOffer, 2); data.SetStep(OfferKind.EndlessGift, 5);
        Assert.That(saves.Flush(), Is.True);
        var restored = new SaveService(directory, 0f).Get<BounceOfferData>();
        Assert.That(restored.Step(OfferKind.EndlessOffer), Is.EqualTo(2));
        Assert.That(restored.Step(OfferKind.EndlessGift), Is.EqualTo(5));
        File.WriteAllText(Path.Combine(directory, "BounceOfferData.json"), "{\"version\":1,\"endlessOfferStep\":-1,\"endlessGiftStep\":0}");
        File.Delete(Path.Combine(directory, "BounceOfferData.json.bak"));
        LogAssert.Expect(LogType.Error, new Regex("invalid save BounceOfferData"));
        var recovered = new SaveService(directory, 0f).Get<BounceOfferData>();
        Assert.That(recovered.Step(OfferKind.EndlessOffer), Is.EqualTo(0), "negative step falls back to a fresh slot");
    }
}
