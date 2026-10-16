using System.Collections.Generic;
using BK.Kit;
using NUnit.Framework;
using UnityEngine;

public sealed class BounceAimTests
{
    private readonly List<GameObject> objects=new List<GameObject>();
    private readonly Vector3[] points=new Vector3[3];

    private void Wall(Vector3 position,Vector3 size)
    {
        var go=new GameObject("Preview test wall");objects.Add(go);go.layer=10;
        go.transform.position=position;go.AddComponent<BoxCollider>().size=size;
        Physics.SyncTransforms();
    }

    [Test]
    public void FirstReflectionStopsAtSecondCollider()
    {
        Wall(new Vector3(3,.5f,0),new Vector3(1,2,30));
        Wall(new Vector3(-3,.5f,0),new Vector3(1,2,30));
        int count=BounceModule.TraceTrajectory(new Vector3(.4f,0,1),points);
        Assert.That(count,Is.EqualTo(3));
        Assert.That(points[1].x,Is.EqualTo(2.25f).Within(.01f));
        Assert.That(points[2].x,Is.EqualTo(-2.25f).Within(.01f));
        Assert.That(points[0].z,Is.EqualTo(BounceModule.DeadZoneZ).Within(.001f));
    }

    [Test]
    public void ReflectedPreviewStopsAtWaterBeforeSubmergedCollider()
    {
        Wall(new Vector3(0,.5f,2),new Vector3(20,2,1));
        Wall(new Vector3(0,.5f,-9),new Vector3(20,2,.2f));
        int count=BounceModule.TraceTrajectory(Vector3.forward,points);
        Assert.That(count,Is.EqualTo(3));
        Assert.That(points[1].z,Is.EqualTo(1.25f).Within(.01f));
        Assert.That(points[2].z,Is.EqualTo(BounceModule.DeadZoneZ).Within(.001f));
        for(int i=0;i<count;i++)Assert.That(points[i].z,Is.GreaterThanOrEqualTo(BounceModule.DeadZoneZ-.001f));
    }

    [Test]
    public void EmptyBoardStartsPreviewAtShoreline()
    {
        Assert.That(BounceModule.TraceTrajectory(Vector3.forward,points),Is.EqualTo(2));
        Assert.That(points[0].z,Is.EqualTo(BounceModule.DeadZoneZ).Within(.001f));
    }

    [Test]
    public void LaunchObstacleDoesNotProduceFalseUnobstructedPreview()
    {
        Wall(new Vector3(0,.5f,-7.2f),new Vector3(2,2,.1f));
        Assert.That(BounceModule.TraceTrajectory(Vector3.forward,points),Is.Zero);
    }

    [Test]
    public void AimRejectsPivotBackwardAndInvalidCoordinatesAndLimitsShallowShots()
    {
        Assert.That(BounceModule.TryGetAim(new Vector3(.1f,0,.1f),out _),Is.False);
        Assert.That(BounceModule.TryGetAim(Vector3.back,out _),Is.False);
        Assert.That(BounceModule.TryGetAim(new Vector3(float.NaN,0,1),out _),Is.False);
        Assert.That(BounceModule.TryGetAim(new Vector3(100,0,1),out var aim),Is.True);
        Assert.That(Vector3.Angle(Vector3.forward,aim),Is.EqualTo(75).Within(.001f));
    }

    [TearDown]
    public void Clean()
    {
        foreach(var go in objects)Object.DestroyImmediate(go);
        objects.Clear();Physics.SyncTransforms();
    }
}
