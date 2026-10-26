using System;
using System.IO;
using System.Linq;
using BK.Kit;
using BK.Kit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BounceReleaseTests
{
    private BounceModule.Levels Load()=>JsonUtility.FromJson<BounceModule.Levels>(File.ReadAllText(BounceReleaseGenerator.DataPath));
    [Test] public void ShippingCampaignHas140ValidNonLoopingStages()
    {
        var library=Load();Assert.That(library.campaign,Is.True);Assert.That(library.levels,Has.Length.EqualTo(140));
        var p=AssetDatabase.LoadAssetAtPath<GamePresentation>(BounceLearningGenerator.PresentationPath);
        Assert.That(AssetDatabase.GetAssetPath(p.levels),Is.EqualTo(BounceReleaseGenerator.DataPath));
        foreach(var l in library.levels)
        {
            Assert.That(BounceLevelLayout.Validate(l),Is.Empty,l.title);
            Assert.That(l.title,Is.Not.Empty);Assert.That(l.intent,Is.Not.Empty);Assert.That(l.chapter,Is.Not.Empty);
            Assert.That(l.rows.Sum(r=>r.Count(char.IsDigit)),Is.GreaterThanOrEqualTo(40));
        }
        Assert.That(library.Resolve(140),Is.SameAs(library.levels[139]));
        Assert.Throws<ArgumentOutOfRangeException>(()=>library.Resolve(141));
        Assert.Throws<ArgumentOutOfRangeException>(()=>library.Resolve(0));
    }
    [Test] public void UnlockBoundariesAndIntroductionsAreExact()
    {
        var library=Load();
        Assert.That(library.levels.Take(13).SelectMany(l=>l.gimmicks),Is.Empty);
        foreach(var type in BounceReleasePolicy.Families)
        {
            int first=Array.FindIndex(library.levels,l=>l.gimmicks.Any(g=>g.type==type))+1;
            Assert.That(first,Is.EqualTo(BounceReleasePolicy.UnlockStage(type)),type);
        }
        for(int i=0;i<library.levels.Length;i++)
        {
            var l=library.levels[i];
            Assert.That(l.introduction,Is.EqualTo(BounceReleasePolicy.IntroducedAt(i+1)));
            Assert.That(l.gimmicks.All(g=>BounceReleasePolicy.IsAllowed(g.type,i+1)),Is.True,"Stage "+(i+1));
            if(!string.IsNullOrEmpty(l.introduction))
                Assert.That(l.gimmicks.Select(g=>g.type=="door"?"switch":g.type).Distinct().Count(),Is.EqualTo(1),"Teach one new rule at a time");
        }
        Assert.That(library.levels.Skip(21).Count(l=>l.gimmicks.Select(g=>g.type=="door"?"switch":g.type).Distinct().Count()>1),Is.GreaterThan(40));
    }
    [Test] public void AuthoredLayoutsAreNotATenStageLoop()
    {
        var levels=Load().levels;
        Assert.That(levels.Select(l=>string.Join("/",l.rows)+"|"+JsonUtility.ToJson(new BounceModule.Level{gimmicks=l.gimmicks})).Distinct().Count(),Is.EqualTo(140));
        Assert.That(levels.Select(l=>l.title.Split('·')[0]).Distinct().Count(),Is.EqualTo(35));
    }
}
