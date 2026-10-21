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
