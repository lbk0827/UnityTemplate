using System;
using System.IO;
using System.Linq;
using BK.Core.Time;
using BK.Kit;
using BK.Meta;
using BK.Save;
using NUnit.Framework;

public sealed class ProgressTests
{
    private string directory;
    [SetUp] public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "BKKitTests", Guid.NewGuid().ToString("N"));
    [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [Test]
    public void CatalogCoversGoldHeartsAndEveryBooster()
    {
        var ids = new BounceCurrencies().Definitions.Select(d => d.Id).ToList();
        Assert.That(ids, Does.Contain(BounceCurrencies.Gold));
        Assert.That(ids, Does.Contain(BounceCurrencies.Heart));
        Assert.That(ids, Does.Contain(BounceCurrencies.InfiniteHeart));
        foreach (var offer in BoosterCatalog.All) Assert.That(ids, Does.Contain(offer.Kind.ToString()));
        var heart = new BounceCurrencies().Definitions.First(d => d.Id == BounceCurrencies.Heart);
        Assert.That(heart.RechargeMax, Is.EqualTo(5));
        Assert.That(heart.RechargeInterval, Is.EqualTo(TimeSpan.FromMinutes(30)));
        var ladder = new[] { 0, 1, 2, 3, 4 }.Select(used => FailContinueLogic.ComputePrice(BounceContinueOffers.BasePrice, BounceContinueOffers.AddPrice, BounceContinueOffers.MaxPrice, used)).ToArray();
        Assert.That(ladder, Is.EqualTo(new long[] { 150, 300, 450, 600, 600 }));
    }

    [Test]
    public void FreshProfileStartsAtStageOneWithFiveHeartsAndFreeEarlyStages()
    {
        var saves = new SaveService(directory, 0f);
        using var wallet = new Wallet(new BounceCurrencies(), saves, new SystemClock());
        using var progress = new StageProgress(saves, wallet, BounceEntry.Policy);
        Assert.That(progress.CurrentStage.CurrentValue, Is.EqualTo(1));
        Assert.That(wallet.ValueOf(BounceCurrencies.Heart), Is.EqualTo(5));
        Assert.That(wallet.ValueOf(BounceCurrencies.Gold), Is.Zero);
        Assert.That(progress.TryStart(3), Is.True);
        Assert.That(wallet.ValueOf(BounceCurrencies.Heart), Is.EqualTo(5), "stages 1-3 are free");
        Assert.That(progress.TryStart(4), Is.True);
        Assert.That(wallet.ValueOf(BounceCurrencies.Heart), Is.EqualTo(4));
        progress.Clear(4);
        Assert.That(wallet.ValueOf(BounceCurrencies.Heart), Is.EqualTo(5), "clearing refunds the heart");
        Assert.That(progress.CurrentStage.CurrentValue, Is.EqualTo(5));
        Assert.That(saves.Flush(), Is.True);
        Assert.That(File.Exists(Path.Combine(directory, "WalletData.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(directory, "StageProgressData.json")), Is.True);
    }

    [Test]
    public void DuplicateResultsDoNotChangeAnAlreadyFinishedRound()
    {
        var session = new GameSession();
        Assert.That(session.Finish(true), Is.False);
        session.Start(2);
        Assert.That(session.Finish(false), Is.True);
        Assert.That(session.Finish(true), Is.False);
        Assert.That(session.State, Is.EqualTo(SessionState.Lost));
        Assert.That(session.Resume(), Is.True, "a bought continue resumes a lost round");
        Assert.That(session.State, Is.EqualTo(SessionState.Playing));
        Assert.That(session.Resume(), Is.False);
        Assert.That(session.Finish(true), Is.True);
        Assert.That(session.Resume(), Is.False, "a won round cannot be resumed");
        session.ReturnToLobby();
        Assert.That(session.State, Is.EqualTo(SessionState.Lobby));
    }
}
