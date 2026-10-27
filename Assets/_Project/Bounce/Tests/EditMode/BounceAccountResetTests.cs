using System;
using System.IO;
using BK.Kit;
using BK.Kit.Editor;
using BK.Meta;
using BK.Save;
using NUnit.Framework;

public sealed class BounceAccountResetTests
{
    private string root;
    [SetUp] public void SetUp() => root = Path.Combine(Path.GetTempPath(), "BounceResetTests", Guid.NewGuid().ToString("N"));
    [TearDown] public void TearDown() { if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Test] public void ArchivesEverySlotAndRecoveryFileAndNextLoadHasFreshDefaults()
    {
        string local = Path.Combine(root, "Bounce", "Profiles", "local");
        using (var saves = new SaveService(local, 0))
        {
            var progress = saves.Get<StageProgressData>(); progress.currentStage = 99; progress.MarkDirty();
            var profile = saves.Get<BounceProfileData>(); profile.playerName = "Previous"; profile.seenGuides.Add("gimmick.mine"); profile.MarkDirty();
            var intent = saves.Get<BouncePurchaseIntent>(); intent.productId = "bounce.welcome"; intent.MarkDirty();
            Assert.That(saves.Flush(), Is.True);
        }
        File.Copy(Path.Combine(local, "StageProgressData.json"), Path.Combine(local, "StageProgressData.json.bak"));
        File.WriteAllText(Path.Combine(local, "FutureSlot.json"), "future data");
        string iap = Path.Combine(root, "Unity", "UnityPurchasing"); Directory.CreateDirectory(iap);
        File.WriteAllText(Path.Combine(iap, "receipt"), "transaction");
        string other = Path.Combine(root, "unrelated.txt"); File.WriteAllText(other, "keep");
        string backup = BounceAccountReset.ArchiveLocalAccount(root);
        Assert.That(Directory.Exists(local), Is.False);
        Assert.That(Directory.Exists(iap), Is.False);
        Assert.That(File.ReadAllText(Path.Combine(backup, "local", "FutureSlot.json")), Is.EqualTo("future data"));
        Assert.That(File.Exists(Path.Combine(backup, "local", "StageProgressData.json.bak")), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(backup, "UnityPurchasing", "receipt")), Is.EqualTo("transaction"));
        Assert.That(File.ReadAllText(other), Is.EqualTo("keep"));
        using (var fresh = new SaveService(local, 0))
        {
            Assert.That(fresh.Get<StageProgressData>().currentStage, Is.EqualTo(1));
            Assert.That(fresh.Get<BounceProfileData>().seenGuides, Is.Empty);
            Assert.That(fresh.Get<BounceProfileData>().playerName, Is.EqualTo(BounceProfileData.DefaultName));
            Assert.That(fresh.Get<BouncePurchaseIntent>().productId, Is.Null.Or.Empty);
            Assert.That(fresh.Get<WalletData>().entries, Is.Empty);
        }
    }

    [Test] public void EmptyAccountCanBeResetRepeatedlyWithoutOverwritingBackups()
    {
        string first = BounceAccountReset.ArchiveLocalAccount(root);
        string second = BounceAccountReset.ArchiveLocalAccount(root);
        Assert.That(first, Is.Not.EqualTo(second));
        Assert.That(Directory.Exists(first), Is.True); Assert.That(Directory.Exists(second), Is.True);
    }
}
