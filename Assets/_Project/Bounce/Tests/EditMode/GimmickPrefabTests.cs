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
    public void CabinetAndFurnaceUseBoxShadingWithBakedAtlasesAndKeepGameplayBindings()
    {
        var presentation=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
        var box=presentation.blocks[0].GetComponentInChildren<Renderer>().sharedMaterial;
        var catalog=presentation.ingameConfig.gimmickPrefabs;
        foreach(var root in new[]{catalog.cupboard.gameObject,catalog.tube.gameObject,catalog.tubeSegment})
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var material=renderer.sharedMaterial;
            Assert.That(material.shader,Is.EqualTo(box.shader),root.name+" matches the Box shading model");
            Assert.That(material.GetTexture("_BaseMap"),Is.Not.Null);
            Assert.That(material.GetTexture("_MatCapTex"),Is.EqualTo(box.GetTexture("_MatCapTex")));
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.uv.Length,Is.EqualTo(mesh.vertexCount),"Baked atlas needs a complete UV channel");
            Assert.That(mesh.uv.All(uv=>uv.x>=0 && uv.x<=1 && uv.y>=0 && uv.y<=1),Is.True);
        }
        Assert.That(box.GetFloat("_EmissionStrength"),Is.Zero,"Existing Box appearance stays unchanged");
        Assert.That(catalog.cupboard.pieces.Length,Is.EqualTo(10));
        Assert.That(catalog.cupboard.solid.size,Is.EqualTo(new Vector3(2.96f,1,1.96f)));
        Assert.That(catalog.tube.pieces.Length,Is.EqualTo(3));
        Assert.That(catalog.tube.solid.size,Is.EqualTo(new Vector3(.96f,1,.96f)));
        Assert.That(catalog.tube.furnaceGlow,Has.Length.EqualTo(1));
        Assert.That(catalog.tube.furnaceGlow[0].sharedMaterial.GetFloat("_EmissionStrength"),Is.GreaterThan(0));
    }

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
