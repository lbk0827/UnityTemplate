using System;
using System.IO;
using BK.Data;
using BK.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BK.Tests
{
    [Serializable]
    public sealed class ProbeRow : ITableRow<int>
    {
        public int id;
        public string name;
        public string[] tags;
        public int Id => id;
    }

    public sealed class ProbeTable : TableAsset<int, ProbeRow> { }

    public sealed class TableImporterTests
    {
        private const string Folder = "Assets/BKFramework/Tests/EditMode/__tmp_tables";

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "Probe.json"),
                "{\"_rows\":[{\"id\":1,\"name\":\"a\",\"tags\":[\"x\"]},{\"id\":2,\"name\":\"b\",\"tags\":[]}]}");
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void ImportsJsonIntoMatchingTableAssetAndUpdatesInPlace()
        {
            var imported = TableImporter.ImportFolder(Folder, Folder, registerAddressables: false);
            Assert.That(imported, Is.EqualTo(new[] { Folder + "/ProbeTable.asset" }));

            var table = AssetDatabase.LoadAssetAtPath<ProbeTable>(Folder + "/ProbeTable.asset");
            Assert.That(table, Is.Not.Null);
            Assert.That(table.Count, Is.EqualTo(2));
            Assert.That(table.Get(2).name, Is.EqualTo("b"));
            Assert.That(table.Get(1).tags, Is.EqualTo(new[] { "x" }));

            File.WriteAllText(Path.Combine(Folder, "Probe.json"), "{\"_rows\":[{\"id\":7,\"name\":\"z\",\"tags\":[]}]}");
            TableImporter.ImportFolder(Folder, Folder, registerAddressables: false);
            var again = AssetDatabase.LoadAssetAtPath<ProbeTable>(Folder + "/ProbeTable.asset");
            Assert.That(ReferenceEquals(again, table), Is.True, "existing asset is updated in place, not replaced");
            Assert.That(again.Count, Is.EqualTo(1));
            Assert.That(again.Get(7).name, Is.EqualTo("z"));
        }
    }
}
