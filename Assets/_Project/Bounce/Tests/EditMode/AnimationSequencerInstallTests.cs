using BrunoMikoski.AnimationSequencer;
using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class AnimationSequencerInstallTests
{
    [Test]
    public void DOTweenAndSequencerTypesResolve()
    {
        Assert.That(DOTween.Version, Does.StartWith("1.3."));
        var go = new GameObject("seq");
        try
        {
            var controller = go.AddComponent<AnimationSequencerController>();
            Assert.That(controller, Is.Not.Null);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [TestCase("Assets/_Project/UI/Sequences/SQ_Popup_Open.prefab")]
    [TestCase("Assets/_Project/UI/Sequences/SQ_Popup_Close.prefab")]
    public void ImportedPopupSequencePrefabsKeepTheirSteps(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        var controller = prefab.GetComponent<AnimationSequencerController>();
        Assert.That(controller, Is.Not.Null, "controller script GUID not remapped");
        Assert.That(controller.AnimationSteps.Length, Is.EqualTo(1));
        Assert.That(controller.AnimationSteps[0], Is.TypeOf<DOTweenAnimationStep>());
        Assert.That(((DOTweenAnimationStep)controller.AnimationSteps[0]).Actions.Length, Is.EqualTo(2));
    }
}
