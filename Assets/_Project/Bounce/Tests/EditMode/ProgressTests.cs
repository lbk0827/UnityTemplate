using System;
using System.IO;
using BK.Kit;
using NUnit.Framework;

public sealed class ProgressTests
{
    private string directory;
    [SetUp] public void SetUp() => directory = Path.Combine(Path.GetTempPath(), "BKKitTests", Guid.NewGuid().ToString("N"));
    [TearDown] public void TearDown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

    [Test]
    public void ProgressSurvivesReloadAndKeepsPreviousSaveAsBackup()
    {
        var store = new LocalSaveStore(directory);
        var data = store.Load();
        Assert.That(data.unlockedLevel, Is.EqualTo(1));
        data.unlockedLevel = 3;
        store.Save(data);
        data.unlockedLevel = 4;
        data.gold=150;
        data.playerName="BK Tester";
        data.missiles=2;data.extraBalls=1;data.bombs=3;data.lasers=4;
        data.soundEnabled = false;
        store.Save(data);
        var restored = new LocalSaveStore(directory).Load();
        Assert.That(restored.unlockedLevel, Is.EqualTo(4));
        Assert.That(restored.gold, Is.EqualTo(150));
        Assert.That(restored.playerName,Is.EqualTo("BK Tester"));
        Assert.That(restored.Count(BoosterKind.Missile),Is.EqualTo(2));
        Assert.That(restored.Count(BoosterKind.ExtraBall),Is.EqualTo(1));
        Assert.That(restored.Count(BoosterKind.Bomb),Is.EqualTo(3));
        Assert.That(restored.Count(BoosterKind.Laser),Is.EqualTo(4));
        Assert.That(restored.soundEnabled, Is.False);
        Assert.That(File.Exists(Path.Combine(directory, "progress.json.bak")), Is.True);
    }

    [Test]
    public void DamagedPrimarySaveRecoversPreviousProgress()
    {
        var store = new LocalSaveStore(directory);
        store.Save(new PlayerProgress { unlockedLevel = 7 });
        store.Save(new PlayerProgress { unlockedLevel = 8 });
        File.WriteAllText(Path.Combine(directory, "progress.json"), "broken json");
        Assert.That(store.Load().unlockedLevel, Is.EqualTo(7));
    }

    [Test]
    public void LegacySoundSettingMigratesWithoutLosingLevel()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"progress.json"),"{\"version\":1,\"unlockedLevel\":9,\"soundEnabled\":false}");
        var store=new LocalSaveStore(directory);var data=store.Load();
        Assert.That(data.unlockedLevel,Is.EqualTo(9));
        Assert.That(data.version,Is.EqualTo(2));
        Assert.That(data.gold,Is.Zero);
        Assert.That(data.playerName,Is.EqualTo("BK Player"));
        foreach(var offer in BoosterCatalog.All)Assert.That(data.Count(offer.Kind),Is.Zero);
        Assert.That(data.musicEnabled,Is.False);Assert.That(data.effectsEnabled,Is.False);
        Assert.That(data.hapticsEnabled,Is.True);Assert.That(data.soundEnabled,Is.True);
        data.musicEnabled=true;data.hapticsEnabled=false;store.Save(data);
        var restored=store.Load();
        Assert.That(restored.musicEnabled,Is.True);Assert.That(restored.effectsEnabled,Is.False);
        Assert.That(restored.hapticsEnabled,Is.False);
    }

    [Test]
    public void InvalidPrimaryAndBackupFallBackToEmptyInventory()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"progress.json"),"{\"version\":2,\"unlockedLevel\":3,\"gold\":-1}");
        File.WriteAllText(Path.Combine(directory,"progress.json.bak"),"{\"version\":2,\"unlockedLevel\":3,\"missiles\":-1}");
        var data=new LocalSaveStore(directory).Load();
        Assert.That(data.unlockedLevel,Is.EqualTo(1));Assert.That(data.gold,Is.Zero);
        Assert.That(data.missiles,Is.Zero);
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
        session.Start(2);
        Assert.That(session.Finish(true), Is.True);
        session.ReturnToLobby();
        Assert.That(session.State, Is.EqualTo(SessionState.Lobby));
    }
}
