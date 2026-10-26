using System;
using System.Collections;
using System.IO;
using System.Reflection;
using BK.Kit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BounceLevelEditorTests
{
    private BounceLevelEditor window;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private object Call(string method, params object[] args) => typeof(BounceLevelEditor).GetMethod(method, Private).Invoke(window, args);
    private FieldInfo Field(string name) => typeof(BounceLevelEditor).GetField(name, Private);
    private BounceLevelDraft Draft => (BounceLevelDraft)Field("draft").GetValue(window);

    [SetUp]
    public void Setup() => window = ScriptableObject.CreateInstance<BounceLevelEditor>();

    [TearDown]
    public void Cleanup()
    {
        window.DiscardChanges();
        UnityEngine.Object.DestroyImmediate(window);
    }

    [Test]
    public void PlaceAndEraseUseTheSameFootprintAsRuntime()
    {
        Call("Add", false);
        var brush = Field("brush"); brush.SetValue(window, Enum.Parse(brush.FieldType, "Tube"));
        Call("Apply", new Vector2Int(3, 5), false);
        Assert.That(Draft.library.levels[^1].gimmicks.Length, Is.EqualTo(1));
        Assert.That(Call("Find", new Vector2Int(2, 5)), Is.EqualTo(0), "Furnace is selectable");
        Call("Apply", new Vector2Int(2, 5), true);
        Assert.That(Draft.library.levels[^1].gimmicks, Is.Empty, "Erasing furnace removes the whole tube");
    }

    [Test]
    public void UndoAddedStageClampsSelectedStageAndKeepsWindowUsable()
    {
        int count = Draft.library.levels.Length;
        Undo.IncrementCurrentGroup(); Call("Add", true); Undo.FlushUndoRecordObjects();
        Assert.That(Draft.library.levels.Length, Is.EqualTo(count + 1));
        Undo.PerformUndo();
        Assert.That(Draft.library.levels.Length, Is.EqualTo(count));
        Assert.That((int)Field("stage").GetValue(window), Is.LessThan(count));
    }

    [Test]
    public void InvalidDraftDoesNotOverwriteLevelFile()
    {
        string before = File.ReadAllText(BounceLevelEditor.DataPath);
        Call("Add", false);
        Assert.That(Call("Save"), Is.EqualTo(false));
        Assert.That(File.ReadAllText(BounceLevelEditor.DataPath), Is.EqualTo(before));
    }

    [Test]
    public void ResizePreservesPaintAndSupportsUndo()
    {
        Call("Add", false);
        Call("SetBlock", new Vector2Int(8, 12), '9');
        var level=Draft.library.levels[^1];
        Assert.That(BK.Kit.BounceLevelLayout.TryResize(level, 19, 24, out _), Is.True);
        Assert.That(level.rows[12][8], Is.EqualTo('9'));
        Assert.That(level.rows, Has.Length.EqualTo(24));
        var before=JsonUtility.ToJson(level);
        Assert.That(BK.Kit.BounceLevelLayout.TryResize(level, 8, 12, out var error), Is.False);
        Assert.That(error, Is.Not.Empty);
        Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before), "Failed shrink must be atomic");
        Undo.IncrementCurrentGroup();Undo.RecordObject(Draft,"Resize test");
        Assert.That(BK.Kit.BounceLevelLayout.TryResize(level, 21, 26, out _), Is.True);
        Undo.FlushUndoRecordObjects();Undo.PerformUndo();
        Assert.That(Draft.library.levels[^1].GridRows, Is.EqualTo(24));
        Assert.That(Draft.library.levels[^1].rows[0].Length, Is.EqualTo(19));
    }

    [Test]
    public void LegacyShortRowsKeepTheirCoordinatesAndTallGridMovesBlocksAndGimmicksTogether()
    {
        var legacy=JsonUtility.FromJson<BK.Kit.BounceModule.Level>("{\"balls\":1,\"rows\":[\"..1..\"]}");
        Assert.That(legacy.GridRows, Is.EqualTo(13));
        Assert.That(BK.Kit.BounceGimmicks.Cell(2,0,5,legacy.GridRows), Is.EqualTo(new Vector3(0,.5f,7)));
        Assert.That(BK.Kit.BounceGimmicks.Cell(2,23,5,24), Is.EqualTo(new Vector3(0,.5f,-5)));
        legacy.gridRows=24;legacy.gimmicks=new[]{new BK.Kit.GimmickSpec {type="mine",col=2,row=20}};
        Assert.That(BK.Kit.BounceLevelLayout.Validate(legacy), Is.Empty);
        Assert.That(BK.Kit.BounceLevelLayout.TryResize(legacy,5,19,out _), Is.False);
        legacy.gimmicks=new[]{new BK.Kit.GimmickSpec {type="panel",col=2,row=20,width=1,travel=.5f}};
        Assert.That(BK.Kit.BounceLevelLayout.TryResize(legacy,5,20,out _), Is.False);
    }

    [Test]
    public void ExternallyChangedFileIsNotOverwritten()
    {
        string before = File.ReadAllText(BounceLevelEditor.DataPath);
        Field("diskText").SetValue(window, "older file contents");
        Assert.That(Call("Save"), Is.EqualTo(false));
        Assert.That(File.ReadAllText(BounceLevelEditor.DataPath), Is.EqualTo(before));
    }

    [UnityTest]
    public IEnumerator WindowRendersEachGimmickInspectorWithoutGuiErrors()
    {
        // Inspector coverage uses the retained gimmick catalogue, not the ten learning boards.
        Draft.library = JsonUtility.FromJson<BK.Kit.BounceModule.Levels>(File.ReadAllText(BounceLearningGenerator.LegacyPath));
        window.Show();
        foreach (int index in new[] {10, 13, 14, 15, 16, 18, 19, 20, 21})
        {
            Field("stage").SetValue(window, index); Field("selected").SetValue(window, 0);
            window.Repaint(); yield return null; yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}
