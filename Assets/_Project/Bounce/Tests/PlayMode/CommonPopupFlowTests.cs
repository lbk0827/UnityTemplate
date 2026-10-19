using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BK.Kit;
using BK.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CommonPopupFlowTests
{
    private string folder;
    [UnityTest]
    public IEnumerator FirstGimmickGuidesQueueAndPersistAcrossSceneReloads()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        KitTestSaves.Seed(folder,level:16,infiniteHearts:true);
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready(true);
        var lobbyView=UnityEngine.Object.FindFirstObjectByType<ImportedSceneView>();
        var hearts=CommonPopupView.Show((RectTransform)lobbyView.transform,lobbyView.presentation,lobbyView.presentation.heart);
        hearts.ConfigureHeart();yield return new WaitForSecondsRealtime(.3f);
        var count=hearts.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t=>t.name=="TXT_HeartCount");
        Assert.That(count.text,Is.EqualTo("\u221e"));
        Assert.That(count.font.HasCharacter('\u221e',searchFallbacks:true),Is.True,"Unlimited lives must have a renderable infinity glyph.");
        typeof(VisualFlowTests).GetMethod("Dump",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"common-heart-infinite",1600});
        hearts.Close();yield return new WaitForSecondsRealtime(.25f);
        KitApp.Instance.Play(16);yield return Ready(false);
        foreach(string type in new[]{"cupboard","tube","panel"})
        {
            float limit=Time.realtimeSinceStartup+5;
            while(UnityEngine.Object.FindFirstObjectByType<CommonPopupView>()==null){Assert.That(Time.realtimeSinceStartup,Is.LessThan(limit));yield return null;}
            Assert.That(Time.timeScale,Is.Zero);Assert.That(KitApp.Instance.HasSeenGuide("gimmick."+type),Is.False);
            var guide=UnityEngine.Object.FindFirstObjectByType<CommonPopupView>();guide.Close();yield return new WaitForSecondsRealtime(.25f);
            Assert.That(KitApp.Instance.HasSeenGuide("gimmick."+type),Is.True);
        }
        Assert.That(Time.timeScale,Is.EqualTo(1));
        UnityEngine.Object.Destroy(KitApp.Instance.gameObject);yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready(true);
        KitApp.Instance.Play(16);yield return Ready(false);yield return new WaitForSecondsRealtime(.3f);
        Assert.That(UnityEngine.Object.FindFirstObjectByType<CommonPopupView>(),Is.Null);
    }
    [UnityTest]
    public IEnumerator ConfirmationAndBoosterFlowNeverSpendOnCancelOrDoubleTap()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        KitTestSaves.Seed(folder,gold:2000,boosters:KitTestSaves.Boosters(missiles:3));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready(true);
        var app=KitApp.Instance;app.Play(1);yield return Ready(false);
        var scene=UnityEngine.Object.FindFirstObjectByType<ImportedSceneView>();
        var module=UnityEngine.Object.FindFirstObjectByType<BounceModule>();
        long hearts=app.Hearts;
        Invoke(scene,"ConfirmExit",true);yield return new WaitForSecondsRealtime(.3f);
        Assert.That(Time.timeScale,Is.Zero);Assert.That(app.Hearts,Is.EqualTo(hearts));
        scene.OnBackRequested();yield return new WaitForSecondsRealtime(.25f);
        Assert.That(app.Hearts,Is.EqualTo(hearts));Assert.That(Time.timeScale,Is.EqualTo(1));
        var offer=BoosterCatalog.Find(BoosterKind.Missile);
        Invoke(scene,"UseBooster",offer);yield return new WaitForSecondsRealtime(.3f);
        Assert.That(app.BoosterCount(BoosterKind.Missile),Is.EqualTo(3));
        scene.OnBackRequested();yield return new WaitForSecondsRealtime(.25f);
        Assert.That(app.HasSeenGuide("booster.Missile"),Is.True);
        Invoke(scene,"UseBooster",offer);yield return new WaitForSecondsRealtime(.3f);
        scene.OnBackRequested();yield return new WaitForSecondsRealtime(.25f);
        Assert.That(app.BoosterCount(BoosterKind.Missile),Is.EqualTo(3));
        Invoke(scene,"UseBooster",offer);yield return new WaitForSecondsRealtime(.3f);
        var popup=UnityEngine.Object.FindFirstObjectByType<CommonPopupView>();
        var use=popup.GetComponentsInChildren<Button>().First(b=>b.name=="Use booster");use.onClick.Invoke();use.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.3f);
        Assert.That(app.BoosterCount(BoosterKind.Missile),Is.EqualTo(2));
        yield return new WaitForSecondsRealtime(2);
        long before=app.Gold;app.Complete(true);yield return new WaitForSecondsRealtime(.3f);
        Assert.That(app.Gold,Is.EqualTo(before+KitApp.ClearGoldReward));
        scene.OnBackRequested();scene.OnBackRequested();yield return new WaitForSecondsRealtime(.3f);
        Assert.That(app.Gold,Is.EqualTo(before+KitApp.ClearGoldReward));
        Assert.That(UnityEngine.Object.FindFirstObjectByType<ClearRewardView>(),Is.Not.Null);
    }
    private static void Invoke(ImportedSceneView scene,string method,object argument)=>typeof(ImportedSceneView).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(scene,new[]{argument});
    private static IEnumerator Ready(bool lobby)
    {
        float end=Time.realtimeSinceStartup+30;
        while(true)
        {
            var scene=UnityEngine.Object.FindFirstObjectByType<ImportedSceneView>();
            if(KitApp.Instance!=null && !KitApp.Instance.IsLoading && scene!=null && scene.lobby==lobby)break;
            Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;
        }
        yield return null;
    }
    [UnityTest]
    public IEnumerator ImportedPopupsHaveValidBindingsAndCloseExactlyOnce()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");
        float deadline=Time.realtimeSinceStartup+30;
        while(KitApp.Instance==null || KitApp.Instance.IsLoading || UnityEngine.Object.FindFirstObjectByType<ImportedSceneView>()==null)
        {Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
        var scene=UnityEngine.Object.FindFirstObjectByType<ImportedSceneView>();var p=scene.presentation;
        var prefabs=new[]{p.heart,p.quit,p.retry,p.unlockGimmick,p.unlockBooster,p.useBooster,p.profile,p.reward,p.celebration};
        foreach(var prefab in prefabs)
        {
            Assert.That(prefab,Is.Not.Null);
            int closed=0;
            var view=CommonPopupView.Show((RectTransform)scene.transform,p,prefab,()=>closed++);
            Assert.That(view.GetComponentsInChildren<MonoBehaviour>(true).All(c=>c!=null),Is.True,prefab.name+" missing script");
            if(prefab==p.heart)view.ConfigureHeart();
            else if(prefab==p.quit || prefab==p.retry)
            {
                view.ConfigureConfirmation(prefab==p.retry,()=>closed++);
                var b=view.Binding(prefab==p.retry?"UIRetryPopup":"UIQuitPopup");
                Assert.That(b.Get<Button>(prefab==p.retry?"retryButton":"basic.quitButton"),Is.Not.Null);
            }
            else if(prefab==p.profile)view.ConfigureProfile();
            else if(prefab==p.reward)view.ConfigureReward("Gold x 100\nMissile x 1");
            else if(prefab==p.celebration)view.ConfigureCelebration();
            else if(prefab==p.useBooster)view.ConfigureUseBooster(BoosterCatalog.Find(BoosterKind.Missile),()=>closed++);
            else view.ConfigureGuide(prefab==p.unlockGimmick?"Furnace":"Missile",prefab==p.unlockGimmick?"Hit the tube to send one block into the furnace.":"Remove one strong block.",prefab==p.unlockGimmick?null:CommonPopupView.BoosterIcon(p,BoosterKind.Missile));
            yield return new WaitForSecondsRealtime(.3f);
            typeof(VisualFlowTests).GetMethod("Dump",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"common-"+prefab.name,1600});
            Assert.That(Time.timeScale,Is.Zero);
            view.Close();view.Close();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(closed,Is.EqualTo(1));Assert.That(Time.timeScale,Is.EqualTo(1));
        }
        KitApp.Instance.MarkGuideSeen("test.guide");
        Assert.That(KitApp.Instance.HasSeenGuide("test.guide"),Is.True);
    }
    [UnityTearDown]
    public IEnumerator Clean()
    {
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        var scope=UnityEngine.Object.FindFirstObjectByType<IntegratedProjectScope>();if(scope!=null)UnityEngine.Object.Destroy(scope.gameObject);
        yield return null;
        foreach(var root in UnityEngine.Object.FindObjectsByType<UIRoot>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(root.gameObject);
        yield return null;
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",null);
        if(folder!=null && Directory.Exists(folder))Directory.Delete(folder,true);
        Time.timeScale=1;
    }
}
