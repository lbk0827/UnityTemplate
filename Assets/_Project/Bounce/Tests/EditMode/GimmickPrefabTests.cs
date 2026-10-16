using System.IO;
using System.Linq;
using BK.Kit;
using BK.Kit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GimmickPrefabTests
{
    [Test]
    public void EveryGimmickHasAnEditablePrefabWithPersistentMaterialAndColliderBindings()
    {
        var config=AssetDatabase.LoadAssetAtPath<IngameConfig>("Assets/_Project/Bounce/Content/IngameConfig.asset");
        Assert.That(config.gimmickPrefabs,Is.Not.Null);
        foreach(var type in BounceLevelLayout.Types)
        {
            var prefab=config.gimmickPrefabs.Find(type);
            Assert.That(prefab,Is.Not.Null,type);
            Assert.That(AssetDatabase.GetAssetPath(prefab),Does.EndWith(".prefab"));
            Assert.That(prefab.solid,Is.Not.Null,type);
            foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)Assert.That(AssetDatabase.Contains(material),Is.True,type);
            Assert.That(prefab.GetComponentsInChildren<TMPro.TMP_Text>(true),Is.Empty);
        }
        Assert.That(config.gimmickPrefabs.cupboard.pieces.Length,Is.EqualTo(10));
        Assert.That(config.gimmickPrefabs.tube.pieces.Length,Is.EqualTo(3));
        Assert.That(config.gimmickPrefabs.tubeSegment,Is.Not.Null);
    }

    [Test]
    public void CreateMissingPrefabsPreservesAuthoredPrefabFiles()
    {
        string path=GimmickPrefabBuilder.Root+"/SeaMine.prefab";
        var before=File.ReadAllBytes(path);
        GimmickPrefabBuilder.Build();
        Assert.That(File.ReadAllBytes(path),Is.EqualTo(before));
    }

    [Test]
    public void SwitchAndDoorRequireMatchingLinksAndArmorRequiresValidDirection()
    {
        var level=new BounceModule.Level {balls=10,rows=Enumerable.Repeat(".........",13).ToArray(),gimmicks=new[] {
            new GimmickSpec {type="switch",col=1,row=5,link="A"},
            new GimmickSpec {type="door",col=4,row=5,link="B"},
            new GimmickSpec {type="armor",col=7,row=5,weakSide=4} }};
        Assert.That(BounceLevelLayout.Validate(level).Count,Is.EqualTo(3));
        level.gimmicks[1].link="A";level.gimmicks[2].weakSide=1;
        Assert.That(BounceLevelLayout.Validate(level),Is.Empty);
    }
}
