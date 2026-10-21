using System;
using BK.Kit;
using NUnit.Framework;
using UnityEngine;

// Fixed boards for existing visual/booster/gimmick regression assertions.
// Production balance tests always use the actually assigned learning catalogue instead.
public sealed class LegacyLevelFixture : IDisposable
{
    private GamePresentation presentation;
    private TextAsset original;
    public LegacyLevelFixture()
    {
#if UNITY_EDITOR
        presentation = UnityEditor.AssetDatabase.LoadAssetAtPath<GamePresentation>("Assets/_Project/Bounce/Content/Presentation.asset");
        original = presentation.levels;
        presentation.levels = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_Project/Bounce/Content/bounce-levels.json");
#else
        Assert.Ignore("Legacy catalogue regression fixtures require the Unity Editor asset database.");
#endif
    }
    public void Dispose() { if (presentation != null) presentation.levels = original; }
}
