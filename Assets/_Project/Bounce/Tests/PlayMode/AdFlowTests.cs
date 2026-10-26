using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BK.Ads;
using BK.Kit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class AdFlowTests
{
    private string folder;
    private int rewardStage;
    private bool settingsChanged;
    private IEnumerator Boot()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        KitTestSaves.Seed(folder,gold:100,hearts:2);
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");
        yield return Wait(()=>KitApp.Instance!=null && !KitApp.Instance.IsLoading && SceneManager.GetActiveScene().name=="VisualLobby");
        yield return Wait(()=>KitApp.Instance.Ads.IsReady(AdFormat.Rewarded));
    }
    [UnityTest] public IEnumerator HeartPopupCancelAndRewardPersistWithoutDoubleGrant()
    {
        yield return Boot();var app=KitApp.Instance;
        var scene=UnityEngine.Object.FindFirstObjectByType<ImportedSceneView>();
        var popup=CommonPopupView.Show((RectTransform)scene.transform,scene.presentation,scene.presentation.heart);
        popup.ConfigureHeart();yield return new WaitForSecondsRealtime(.3f);
        var ad=popup.Binding("HeartRechargeExtension").Get<Button>("_adButton");
        Assert.That(ad.gameObject.activeInHierarchy && ad.interactable,Is.True);
        Capture("ads-heart");
        long before=app.Hearts;ad.onClick.Invoke();
        yield return Wait(()=>GameObject.Find("MaxRewardedCloseButton")!=null);
        popup.Close();Assert.That(popup.IsClosing,Is.False);
        Assert.That(AudioListener.pause,Is.True);
        Click("MaxRewardedCloseButton");yield return Wait(()=>!app.IsAdBusy);
        Assert.That(app.Hearts,Is.EqualTo(before));
        Assert.That(Time.timeScale,Is.Zero);Assert.That(AudioListener.pause,Is.False);
        yield return null;yield return Wait(()=>app.Ads.IsReady(AdFormat.Rewarded));
        ad.onClick.Invoke();
        yield return Wait(()=>GameObject.Find("MaxRewardButton")!=null);
        Assert.That(app.WatchHeartAdAsync().GetAwaiter().GetResult(),Is.False,"Concurrent request rejected");
        Click("MaxRewardButton");Click("MaxRewardedCloseButton");
        yield return Wait(()=>!app.IsAdBusy);
        Assert.That(app.Hearts,Is.EqualTo(before+1));Assert.That(KitTestSaves.Hearts(folder),Is.EqualTo(before+1));
        popup.Close();yield return new WaitForSecondsRealtime(.3f);Assert.That(Time.timeScale,Is.EqualTo(1));
    }
    [UnityTest] public IEnumerator ClearDoubleButtonPreservesBaseOnCancelAndSavesBonusOnce()
    {
        yield return Boot();var app=KitApp.Instance;
        rewardStage=app.AdSettings.firstRewardStage;settingsChanged=true;app.AdSettings.firstRewardStage=1;
        app.Play(1);yield return Wait(()=>!app.IsLoading && SceneManager.GetActiveScene().name=="VisualIngame");
        long before=app.Gold;app.Complete(true);
        Assert.That(KitTestSaves.Gold(folder),Is.EqualTo(before+KitApp.ClearGoldReward));
        yield return Wait(()=>UnityEngine.Object.FindFirstObjectByType<ClearRewardView>()!=null);
        var reward=UnityEngine.Object.FindFirstObjectByType<ClearRewardView>();
        var binding=reward.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="StageClearPopup");
        var button=binding.Get<Button>("basic.adRewardButton");
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(button.gameObject.activeInHierarchy,Is.True);Capture("ads-clear-double");
        button.onClick.Invoke();yield return Wait(()=>GameObject.Find("MaxRewardedCloseButton")!=null);
        Click("MaxRewardedCloseButton");yield return Wait(()=>!app.IsAdBusy);
        Assert.That(reward.HasStarted,Is.False);Assert.That(button.interactable,Is.True);
        Assert.That(KitTestSaves.Gold(folder),Is.EqualTo(before+KitApp.ClearGoldReward));
        yield return null;yield return Wait(()=>app.Ads.IsReady(AdFormat.Rewarded));
        button.onClick.Invoke();yield return Wait(()=>GameObject.Find("MaxRewardButton")!=null);
        Click("MaxRewardButton");Click("MaxRewardedCloseButton");yield return Wait(()=>!app.IsAdBusy);
        Assert.That(app.WatchDoubleClearAdAsync().GetAwaiter().GetResult(),Is.False);
        Assert.That(KitTestSaves.Gold(folder),Is.EqualTo(before+2*KitApp.ClearGoldReward));
        yield return Wait(()=>SceneManager.GetActiveScene().name=="VisualLobby" && !app.IsLoading);
        Assert.That(app.Gold,Is.EqualTo(before+2*KitApp.ClearGoldReward));
    }
    private static void Click(string name)=>GameObject.Find(name).GetComponent<Button>().onClick.Invoke();
    private static void Capture(string name)=>typeof(VisualFlowTests).GetMethod("Dump",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{name,1600});
    private static IEnumerator Wait(Func<bool> ready)
    {
        float until=Time.realtimeSinceStartup+30;
        while(!ready() && Time.realtimeSinceStartup<until)yield return null;
        Assert.That(ready(),Is.True,"Advertisement flow timed out");
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(KitApp.Instance!=null)
        {
            if(settingsChanged)KitApp.Instance.AdSettings.firstRewardStage=rewardStage;
            UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        }
        settingsChanged=false;yield return null;
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",null);
        Time.timeScale=1;AudioListener.pause=false;
    }
}
