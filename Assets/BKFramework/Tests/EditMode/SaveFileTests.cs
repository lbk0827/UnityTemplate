using System.IO;
using BK.Save;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class SaveFileTests
    {
        private string _dir;
        private string Path(string n) => System.IO.Path.Combine(_dir, n);

        [SetUp] public void SetUp() { _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BKSaveTests", System.Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [Test]
        public void WriteAtomicRotatesPreviousFileIntoBackup()
        {
            SaveFile.WriteAtomic(Path("a.json"), "{\"version\":1}");
            SaveFile.WriteAtomic(Path("a.json"), "{\"version\":2}");
            Assert.That(File.ReadAllText(Path("a.json")), Is.EqualTo("{\"version\":2}"));
            Assert.That(File.ReadAllText(Path("a.json.bak")), Is.EqualTo("{\"version\":1}"));
            Assert.That(File.Exists(Path("a.json.tmp")), Is.False);
        }

        [Test]
        public void ReadUsablePrefersPrimaryThenBackup()
        {
            SaveFile.WriteAtomic(Path("a.json"), "{\"version\":1}");
            SaveFile.WriteAtomic(Path("a.json"), "{\"version\":2}");
            Assert.That(SaveFile.ReadUsable(Path("a.json"), out var corrupt), Is.EqualTo("{\"version\":2}"));
            Assert.That(corrupt, Is.False);

            File.WriteAllText(Path("a.json"), "{\"version\":");
            Assert.That(SaveFile.ReadUsable(Path("a.json"), out corrupt), Is.EqualTo("{\"version\":1}"));
            Assert.That(corrupt, Is.True);
        }

        [Test]
        public void ReadUsableReturnsNullWhenNothingParses()
        {
            File.WriteAllText(Path("a.json"), "nope");
            File.WriteAllText(Path("a.json.bak"), "{\"noVersion\":true}");
            Assert.That(SaveFile.ReadUsable(Path("a.json"), out var corrupt), Is.Null);
            Assert.That(corrupt, Is.True);
        }

        [Test]
        public void PreserveCorruptMovesFileAsideInsteadOfDeleting()
        {
            File.WriteAllText(Path("a.json"), "nope");
            var preserved = SaveFile.PreserveCorrupt(Path("a.json"));
            Assert.That(File.Exists(Path("a.json")), Is.False);
            Assert.That(preserved, Does.StartWith(Path("a.json.corrupt-")));
            Assert.That(File.ReadAllText(preserved), Is.EqualTo("nope"));
        }

        [Test]
        public void TryReadVersionRejectsInvalidJsonAndMissingVersion()
        {
            Assert.That(SaveFile.TryReadVersion("{\"version\":3}", out var v), Is.True);
            Assert.That(v, Is.EqualTo(3));
            Assert.That(SaveFile.TryReadVersion("{\"x\":3}", out _), Is.False);
            Assert.That(SaveFile.TryReadVersion("{", out _), Is.False);
            Assert.That(SaveFile.TryReadVersion("", out _), Is.False);
        }
    }
}
