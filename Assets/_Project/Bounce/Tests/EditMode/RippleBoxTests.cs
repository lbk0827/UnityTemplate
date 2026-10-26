using System.Linq;
using BK.Kit;
using BK.Kit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RippleBoxTests
{
    [Test]
    public void OriginalBoxCollisionAndColourOrderSurviveTheVisualReplacement()
    {
        var p=AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
        Assert.That(p.blocks.Length,Is.EqualTo(9));Assert.That(p.brokenBlocks.Length,Is.EqualTo(9));
        for(int i=0;i<4;i++)
        {
            var original=RippleBoxSetup.Original(i);
            var collider=p.blocks[i].GetComponentInChildren<MeshCollider>();
            var previous=original.GetComponentInChildren<MeshCollider>();
            Assert.That(collider.sharedMesh,Is.EqualTo(previous.sharedMesh));
            Assert.That(collider.convex,Is.EqualTo(previous.convex));
            Assert.That(collider.transform.localPosition,Is.EqualTo(previous.transform.localPosition));
            var renderer=p.blocks[i].GetComponentInChildren<MeshRenderer>();
            var reference=original.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.That(renderer.sharedMaterial.shader,Is.EqualTo(reference.shader));
            var tintDifference=(Vector4)(renderer.sharedMaterial.GetColor("_BaseColor")-reference.GetColor("_BaseColor"));
            Assert.That(tintDifference.sqrMagnitude,Is.LessThan(1e-10f),"Colour survives material serialization");
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(AssetDatabase.GetAssetPath(mesh),Does.StartWith(RippleBoxSetup.Root));
            Assert.That(mesh.uv.Length,Is.EqualTo(mesh.vertexCount));
            Assert.That(mesh.bounds.size.x,Is.LessThanOrEqualTo(1.01f));
            Assert.That(mesh.bounds.size.z,Is.LessThanOrEqualTo(1.01f));
            var pieces=p.brokenBlocks[i].GetComponentsInChildren<MeshRenderer>();
            Assert.That(pieces.Length,Is.EqualTo(3));
            Assert.That(pieces.All(r=>r.sharedMaterial==renderer.sharedMaterial),Is.True);
            Assert.That(p.brokenBlocks[i].GetComponentsInChildren<Collider>(),Is.Empty);
        }
        for(int i=4;i<9;i++)
        {
            var material=p.blocks[i].GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.That(((Vector4)(material.GetColor("_BaseColor")-BounceBlockPalette.Colours[i])).sqrMagnitude, Is.LessThan(1e-10f));
            Assert.That(p.blocks[i].GetComponentInChildren<MeshCollider>().sharedMesh,
                Is.EqualTo(p.blocks[0].GetComponentInChildren<MeshCollider>().sharedMesh));
            Assert.That(p.brokenBlocks[i].GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterial==material),Is.True);
        }
        Assert.That(p.blocks.Select(b=>b.GetComponentInChildren<MeshRenderer>().sharedMaterial.GetColor("_BaseColor")).Distinct().Count(),Is.EqualTo(9));
    }
}
