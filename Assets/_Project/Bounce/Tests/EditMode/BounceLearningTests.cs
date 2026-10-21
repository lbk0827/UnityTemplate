using System.IO;
using System.Linq;
using BK.Kit;
using BK.Kit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class BounceLearningTests
{
    [Test]
    public void SeedReproducesTheAppliedTenDistinctLearningBoards()
    {
        var expected = BounceLearningGenerator.Create(BounceLearningGenerator.Seed);
        var actual = JsonUtility.FromJson<BounceModule.Levels>(File.ReadAllText(BounceLearningGenerator.DataPath));
        Assert.That(JsonUtility.ToJson(actual), Is.EqualTo(JsonUtility.ToJson(expected)));
        Assert.That(actual.levels, Has.Length.EqualTo(10));
        Assert.That(actual.levels.Select(l => string.Join("/", l.rows)+string.Join(";",l.gimmicks.Select(JsonUtility.ToJson))).Distinct().Count(), Is.EqualTo(10));
        var presentation = AssetDatabase.LoadAssetAtPath<GamePresentation>(BounceLearningGenerator.PresentationPath);
        Assert.That(AssetDatabase.GetAssetPath(presentation.levels), Is.EqualTo(BounceLearningGenerator.DataPath));
        foreach (var level in actual.levels)
        {
            Assert.That(BounceLevelLayout.Validate(level), Is.Empty);
            Assert.That(level.rows.Length, Is.EqualTo(13));
            Assert.That(level.rows.All(r => r.Length == 9), Is.True);
            Assert.That(level.rows.Skip(8).All(r => r == "........."), Is.True, "Leave the launch area clear");
        }
    }

    [Test]
    public void FirstGimmickIsIntroducedAloneAndRepeatedWithoutChangingBoxDurability()
    {
        var levels = BounceLearningGenerator.Create(BounceLearningGenerator.Seed).levels;
        Assert.That(levels.Take(5).All(l => l.gimmicks.Length == 0), Is.True);
        Assert.That(levels[5].gimmicks, Has.Length.EqualTo(1));
        Assert.That(levels.Skip(5).SelectMany(l => l.gimmicks).All(g => g.type == "mine"), Is.True);
        Assert.That(levels.Skip(6).Count(l => l.gimmicks.Any(g => g.type == "mine")), Is.GreaterThanOrEqualTo(3));
        var guide = ImportedSceneView.GimmickGuide("mine");
        Assert.That(guide.title, Is.EqualTo("Sea mine"));
        Assert.That(guide.body, Does.Contain("eight surrounding cells"));
        Assert.That(guide.body, Does.Not.Contain("panel"));
    }
}
