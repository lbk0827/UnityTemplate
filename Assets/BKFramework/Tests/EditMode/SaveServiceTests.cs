using System.IO;
using System.Text.RegularExpressions;
using BK.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BK.Tests
{
    public sealed class SaveServiceTests
    {
        private string _dir;
        [SetUp] public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", System.Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [System.Serializable]
        private sealed class Progress : SaveData
        {
            public override int CurrentVersion => 2;
            public int level = 1;
            public int gold;
            public bool music = true, sfx = true;
            [System.NonSerialized] public bool created;
            [System.NonSerialized] public int migratedFrom = -1;
            public override void OnCreate() => created = true;
            public override void OnMigrate(int fromVersion)
            {
                migratedFrom = fromVersion;
                if (fromVersion < 2) sfx = music; // v1 had a single flag
            }
            public override bool Validate() => level >= 1 && gold >= 0;
        }

        private string File_ => Path.Combine(_dir, "Progress.json");

        [Test]
        public void GetCreatesDefaultsAndFlushWritesVersionedJson()
        {
            var saves = new SaveService(_dir);
            var p = saves.Get<Progress>();
            Assert.That(p.created, Is.True);
            Assert.That(p.version, Is.EqualTo(2));
            Assert.That(saves.HasUnsavedChanges, Is.True, "a freshly created slot is dirty so the file appears on first flush");
            Assert.That(saves.Flush(), Is.True);
            Assert.That(SaveFile.TryReadVersion(File.ReadAllText(File_), out var v) && v == 2, Is.True);
            Assert.That(saves.HasUnsavedChanges, Is.False);
            Assert.That(ReferenceEquals(saves.Get<Progress>(), p), Is.True, "same instance on repeated Get");
        }

        [Test]
        public void DirtySlotRoundTripsAndKeepsBackup()
        {
            var saves = new SaveService(_dir);
            var p = saves.Get<Progress>();
            saves.Flush();
            p.level = 5; p.gold = 120; p.MarkDirty();
            Assert.That(saves.Flush(), Is.True);

            var again = new SaveService(_dir).Get<Progress>();
            Assert.That(again.level, Is.EqualTo(5));
            Assert.That(again.gold, Is.EqualTo(120));
            Assert.That(again.created, Is.False);
            Assert.That(File.Exists(File_ + SaveFile.BackupExtension), Is.True);
        }

        [Test]
        public void UnchangedSlotIsNotRewritten()
        {
            var saves = new SaveService(_dir);
            saves.Get<Progress>(); saves.Flush();
            var stamp = File.GetLastWriteTimeUtc(File_);
            File.SetLastWriteTimeUtc(File_, stamp.AddMinutes(-5));
            Assert.That(saves.Flush(), Is.True);
            Assert.That(File.GetLastWriteTimeUtc(File_), Is.EqualTo(stamp.AddMinutes(-5)));
        }

        [Test]
        public void OlderFileIsMigratedAndMarkedDirty()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(File_, "{\"version\":1,\"level\":9,\"music\":false}");
            var saves = new SaveService(_dir);
            var p = saves.Get<Progress>();
            Assert.That(p.level, Is.EqualTo(9));
            Assert.That(p.migratedFrom, Is.EqualTo(1));
            Assert.That(p.sfx, Is.False);
            Assert.That(p.version, Is.EqualTo(2));
            Assert.That(p.IsDirty, Is.True);
        }

        [Test]
        public void CorruptPrimaryFallsBackToBackupAndPreservesEvidence()
        {
            var saves = new SaveService(_dir);
            var p = saves.Get<Progress>(); saves.Flush();
            p.level = 7; p.MarkDirty(); saves.Flush();
            File.WriteAllText(File_, "{\"version\":2,\"level\":");
            LogAssert.Expect(LogType.Error, new Regex("corrupt save Progress"));
            var again = new SaveService(_dir).Get<Progress>();
            Assert.That(again.level, Is.EqualTo(1), "backup holds the save before level=7");
            Assert.That(Directory.GetFiles(_dir, "Progress.json.corrupt-*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void FailedValidationIsTreatedAsCorrupt()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(File_, "{\"version\":2,\"level\":3,\"gold\":-1}");
            LogAssert.Expect(LogType.Error, new Regex("invalid save Progress"));
            var p = new SaveService(_dir).Get<Progress>();
            Assert.That(p.gold, Is.Zero);
            Assert.That(p.level, Is.EqualTo(1));
            Assert.That(p.created, Is.True);
            Assert.That(Directory.GetFiles(_dir, "Progress.json.corrupt-*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void DisposeFlushesDirtySlots()
        {
            var saves = new SaveService(_dir);
            var p = saves.Get<Progress>();
            p.gold = 42; p.MarkDirty();
            saves.Dispose();
            Assert.That(new SaveService(_dir).Get<Progress>().gold, Is.EqualTo(42));
        }
    }
}
