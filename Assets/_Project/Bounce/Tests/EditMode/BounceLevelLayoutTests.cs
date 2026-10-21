using System.IO;
using System.Linq;
using BK.Kit;
using NUnit.Framework;
using UnityEngine;

public sealed class BounceLevelLayoutTests
{
    private static BounceModule.Level Empty() => new BounceModule.Level
    {
        balls = 20, rows = Enumerable.Repeat(".........", 13).ToArray(),
        gimmicks = new[] { new GimmickSpec { type = "tube", col = 3, row = 5, length = 3 } }
    };

    [Test]
    public void FurnaceIsIncludedInBothOrientations()
    {
        var spec = Empty().gimmicks[0];
        Assert.That(BounceLevelLayout.Cells(spec).First(), Is.EqualTo(new Vector2Int(2, 5)));
        Assert.That(BounceLevelLayout.Cells(spec).Count(), Is.EqualTo(4));
        spec.vertical = true;
        Assert.That(BounceLevelLayout.Cells(spec).First(), Is.EqualTo(new Vector2Int(3, 4)));
        Assert.That(BounceLevelLayout.Cells(spec).Last(), Is.EqualTo(new Vector2Int(3, 7)));
    }

    [Test]
    public void FurnaceCannotExtendOutsideBoardOrOverlapABlock()
    {
        var level = Empty(); level.gimmicks[0].col = 0;
        Assert.That(BounceLevelLayout.Validate(level), Is.Not.Empty);
        level = Empty(); level.rows[5] = "..1......";
        Assert.That(BounceLevelLayout.Validate(level).Any(e => e.Contains("겹칩니다")), Is.True);
    }

    [Test]
    public void PanelSweepMustBeClearAndStayWithinWalls()
    {
        var level = Empty();
        var panel = new GimmickSpec { type = "panel", col = 4, row = 9, travel = 2, width = 2, speed = 1 };
        level.gimmicks = level.gimmicks.Concat(new[] { panel }).ToArray();
        Assert.That(BounceLevelLayout.Validate(level), Is.Empty);
        level.rows[9] = "..1......";
        Assert.That(BounceLevelLayout.Validate(level).Any(e => e.Contains("왕복 경로")), Is.True);
        level.rows[9] = "........."; panel.travel = 5;
        Assert.That(BounceLevelLayout.Validate(level).Any(e => e.Contains("벽 밖")), Is.True);
        panel.travel = 2; panel.speed = float.NaN;
        Assert.That(BounceLevelLayout.Validate(level), Is.Not.Empty);
    }

    [Test]
    public void CupboardRequiresTenDistinctAdjacentBlocks()
    {
        var level = Empty(); level.gimmicks = new[] { new GimmickSpec { type = "cupboard", col = 3, row = 4 } };
        Assert.That(BounceLevelLayout.Validate(level).Any(e => e.Contains("인접 블록 10개")), Is.True);
        level.rows[3] = "...222..."; level.rows[4] = "..2...2..";
        level.rows[5] = "..2...2.."; level.rows[6] = "...222...";
        Assert.That(BounceLevelLayout.Validate(level), Is.Empty);
    }

    [TestCase("Assets/_Project/Bounce/Content/bounce-levels.json")]
    [TestCase("Assets/_Project/Bounce/Content/bounce-learning-levels.json")]
    public void EveryAuthoredStageValidatesAndSurvivesJsonRoundTrip(string path)
    {
        var library = JsonUtility.FromJson<BounceModule.Levels>(File.ReadAllText(path));
        var copy = JsonUtility.FromJson<BounceModule.Levels>(JsonUtility.ToJson(library, true));
        Assert.That(copy.levels.Length, Is.EqualTo(library.levels.Length));
        for (int i = 0; i < copy.levels.Length; i++)
        {
            Assert.That(BounceLevelLayout.Validate(copy.levels[i]), Is.Empty, "Stage " + (i + 1));
            Assert.That(JsonUtility.ToJson(copy.levels[i]), Is.EqualTo(JsonUtility.ToJson(library.levels[i])));
        }
    }
}
