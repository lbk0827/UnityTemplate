using System.IO;
using BK.Options;
using BK.Save;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class OptionsServiceTests
    {
        private string _dir;
        [SetUp] public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "BKSaveTests", System.Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [Test]
        public void DefaultsAreOnAndLanguageFallsBackToDefault()
        {
            using var options = new OptionsService(new SaveService(_dir), "ko");
            Assert.That(options.Music.Value, Is.True);
            Assert.That(options.Sfx.Value, Is.True);
            Assert.That(options.Haptics.Value, Is.True);
            Assert.That(options.Language.Value, Is.EqualTo("ko"));
        }

        [Test]
        public void ChangesPersistThroughTheSaveService()
        {
            var saves = new SaveService(_dir);
            using (var options = new OptionsService(saves, "en"))
            {
                options.Music.Value = false;
                options.Language.Value = "ja";
                Assert.That(saves.HasUnsavedChanges, Is.True);
                saves.Flush();
            }
            using var reloaded = new OptionsService(new SaveService(_dir), "en");
            Assert.That(reloaded.Music.Value, Is.False);
            Assert.That(reloaded.Sfx.Value, Is.True);
            Assert.That(reloaded.Language.Value, Is.EqualTo("ja"));
        }
    }
}
