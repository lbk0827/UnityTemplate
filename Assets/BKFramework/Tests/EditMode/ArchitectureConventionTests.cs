using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace BK.Tests
{
    /// <summary>Source-level guards for rules no compiler enforces (sf's *ContractTests pattern).</summary>
    public sealed class ArchitectureConventionTests
    {
        private static readonly string Root = Path.Combine(Application.dataPath, "BKFramework", "Runtime");

        private static IEnumerable<string> RuntimeSources()
            => Directory.EnumerateFiles(Root, "*.cs", SearchOption.AllDirectories);

        private static IEnumerable<string> ProjectSources()
            => Directory.EnumerateFiles(Path.Combine(Application.dataPath, "_Project"), "*.cs", SearchOption.AllDirectories);

        private static string Relative(string path) => Path.GetRelativePath(Application.dataPath, path);

        [Test]
        public void OnlyBackInputDriverReadsTheEscapeKey()
        {
            var offenders = RuntimeSources().Concat(ProjectSources())
                .Where(f => !f.EndsWith("BackInputDriver.cs"))
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"KeyCode\.Escape|escapeKey"))
                .Select(Relative)
                .ToList();
            Assert.That(offenders, Is.Empty, "route back input through IUIService.HandleBackRequest");
        }

        [Test]
        public void FrameworkNeverReadsTheSystemClockDirectly()
        {
            var offenders = RuntimeSources()
                .Where(f => !f.EndsWith("SystemClock.cs") && !f.EndsWith("SaveFile.cs"))
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"DateTime\.(UtcNow|Now)\b"))
                .Select(Relative)
                .ToList();
            Assert.That(offenders, Is.Empty, "inject IClock instead of reading DateTime directly");
        }

        [Test]
        public void FrameworkAssembliesOnlyDependDownward()
        {
            var allowed = new Dictionary<string, string[]>
            {
                ["BK.Core"] = new string[0],
                ["BK.Save"] = new[] { "BK.Core" },
                ["BK.Options"] = new[] { "BK.Core", "BK.Save" },
                ["BK.Meta"] = new[] { "BK.Core", "BK.Save" },
                ["BK.Assets"] = new[] { "BK.Core" },
                ["BK.Scene"] = new[] { "BK.Core", "BK.Assets" },
                ["BK.Data"] = new[] { "BK.Core", "BK.Assets" },
                ["BK.UI"] = new[] { "BK.Core", "BK.Assets", "BK.Scene" },
                ["BK.Localization"] = new[] { "BK.Core", "BK.Assets", "BK.Data" },
                ["BK.Presentation"] = new[] { "BK.Core" },
            };

            foreach (var asmdef in Directory.EnumerateFiles(Root, "*.asmdef", SearchOption.AllDirectories))
            {
                var probe = JsonUtility.FromJson<AsmdefProbe>(File.ReadAllText(asmdef));
                if (probe.name == "BK.Composition")
                    continue; // the composition root may see every layer
                Assert.That(allowed.ContainsKey(probe.name), Is.True,
                    $"{probe.name} is not in the layer map; add it with its allowed references");
                var bad = probe.references.Where(r => r.StartsWith("BK.") && !allowed[probe.name].Contains(r)).ToList();
                Assert.That(bad, Is.Empty, $"{probe.name} references {string.Join(", ", bad)}");
            }
        }

        [System.Serializable]
        private sealed class AsmdefProbe
        {
            public string name;
            public string[] references = new string[0];
        }
    }
}
