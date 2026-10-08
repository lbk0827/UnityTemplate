using System;
using System.Collections;
using System.IO;
using System.Linq;
using BK.Kit;
using BK.UI;
using BK.Scene;
using VContainer;
using TMPro;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class VisualFlowTests
{
    private string folder;
    [UnityTest]
    public IEnumerator ImportedLobbyStartsBounceAndSupportsRetryAndReturn()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");
        yield return Ready("VisualLobby");
        var navigation=UnityEngine.Object.FindFirstObjectByType<LobbyNavigation>();
        var hud=UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.role=="UIHUDPanel");
        var store=hud.Get<Toggle>("StoreToggle");var home=hud.Get<Toggle>("HomeToggle");
        Assert.That(home.isOn,Is.True);
        Assert.That(store.targetGraphic.raycastTarget,Is.True);
        Click(store);
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(navigation.SelectedPage,Is.EqualTo(0));
        var pages=UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(r=>r.name=="svl_lobby");
        Assert.That(pages.horizontalNormalizedPosition,Is.EqualTo(0).Within(.001f));
        Assert.That(home.isOn,Is.False);
        Dump("store");
        hud.Get<Toggle>("ContentToggle").isOn=true;
        Click(home);
        yield return new WaitForSecondsRealtime(.35f);
        Assert.That(navigation.SelectedPage,Is.EqualTo(1));
        Assert.That(navigation.IsMoving,Is.False);
        Assert.That(pages.horizontalNormalizedPosition,Is.EqualTo(.5f).Within(.001f));
        Dump("lobby");
        Canvas.ForceUpdateCanvases();
        Assert.That(pages.horizontalNormalizedPosition,Is.EqualTo(.5f).Within(.001f),"Resizing capture canvas must preserve the selected Home page");
        var stage=UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.role=="StageInfo");
        var start=stage.Get<Button>("buttons.0.button");
        Assert.That(start,Is.Not.Null,"Original stage start binding must survive import");
        Assert.That(start.gameObject.activeInHierarchy,Is.True,"Original play button must be visible");
        Assert.That(start.interactable,Is.True);
        start.onClick.Invoke();
        yield return Ready("VisualIngame");
        yield return new WaitForSeconds(.6f);
        Dump("ingame");
        var game=UnityEngine.Object.FindFirstObjectByType<BounceModule>();
        Assert.That(game.BlocksRemaining,Is.EqualTo(24));
        Assert.That(game.BallsRemaining,Is.EqualTo(20));
        Assert.That(game.Fire(Vector3.forward),Is.True);
        Assert.That(game.BallsRemaining,Is.EqualTo(19));
        yield return new WaitForSeconds(1);
        Assert.That(game.BlocksRemaining,Is.LessThan(24),"A physical shot must hit a block");
        KitApp.Instance.Play(1);
        yield return Ready("VisualIngame");
        Assert.That(UnityEngine.Object.FindFirstObjectByType<BounceModule>().BallsRemaining,Is.EqualTo(20));
        KitApp.Instance.Complete(true);
        Assert.That(KitApp.Instance.Progress.gold,Is.EqualTo(50));
        KitApp.Instance.Complete(true);KitApp.Instance.Complete(false);
        Assert.That(KitApp.Instance.Progress.gold,Is.EqualTo(50),"Repeated results cannot grant more gold");
        Assert.That(new LocalSaveStore(folder).Load().gold,Is.EqualTo(50),"Reward is saved before Continue");
        yield return new WaitForSeconds(.4f);
        Dump("result");
        var result=UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.role=="StageClearPopup");
        Assert.That(result.Get<TMP_Text>("rewardAmountText").text,Is.EqualTo("50"));
        var claim=result.Get<Button>("basic.claimButton");
        Assert.That(claim.gameObject.activeInHierarchy,Is.True);
        Assert.That(result.Get<TMP_Text>("goldAmountText").gameObject.activeInHierarchy,Is.True);
        Assert.That(result.Get<TMP_Text>("goldAmountText").text,Is.EqualTo("0"),"Display starts at pre-reward balance; save already contains 50");
        yield return null;Click(claim);
        var rewardView=UnityEngine.Object.FindFirstObjectByType<ClearRewardView>();
        Assert.That(rewardView.IsCollecting,Is.True);
        claim.onClick.Invoke();result.Get<Button>("closeButton").onClick.Invoke();
        Assert.That(claim.interactable,Is.False);
        Assert.That(KitApp.Instance.IsLoading,Is.False,"Return waits for collection animation");
        yield return new WaitForSecondsRealtime(.35f);Dump("reward-flight");
        Assert.That(rewardView!=null && rewardView.IsCollecting,Is.True,"Original coin skin must support the flight, not skip it");
        Assert.That(GameObject.Find("Reward flight"),Is.Not.Null);
        Assert.That(KitApp.Instance.Progress.gold,Is.EqualTo(50));
        Assert.That(new LocalSaveStore(folder).Load().gold,Is.EqualTo(50));
        yield return Ready("VisualLobby");
        Assert.That(KitApp.Instance.Progress.unlockedLevel,Is.EqualTo(2));
        var path=UnityEngine.Object.FindFirstObjectByType<StagePathView>();
        Assert.That(path.IsAdvancing,Is.True);
        Assert.That(GameObject.Find("Stage_Slot_1"),Is.Not.Null);
        var movingStage=UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsSortMode.None).First(b=>b.role=="StageInfo");
        movingStage.Get<Button>("buttons.0.button").onClick.Invoke();
        Assert.That(KitApp.Instance.IsLoading,Is.False,"Play must wait until the slot transition finishes");
        yield return new WaitForSecondsRealtime(1);
        Assert.That(path.IsAdvancing,Is.False);
        Assert.That(GameObject.Find("Stage_Slot_2"),Is.Not.Null);
        Assert.That(GameObject.Find("Stage_Slot_1"),Is.Null);
        Dump("advanced-lobby");
        KitApp.Instance.GoToLobby();
        yield return Ready("VisualLobby");
        Assert.That(UnityEngine.Object.FindFirstObjectByType<StagePathView>().IsAdvancing,Is.False,"A consumed transition must not replay");
        var top=UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsSortMode.None).First(b=>b.role=="UIHUDSub_Top" && b.Get<VisualBindings>("gold")!=null);
        Assert.That(top.Get<VisualBindings>("gold").Get<TMP_Text>("currencyCountText").text,Is.EqualTo("50"));
        UnityEngine.Object.Destroy(KitApp.Instance.gameObject);yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        Assert.That(KitApp.Instance.Progress.gold,Is.EqualTo(50));
        KitApp.Instance.Play(2);yield return Ready("VisualIngame");
        KitApp.Instance.Complete(false);
        Assert.That(KitApp.Instance.LastGoldReward,Is.Zero);Assert.That(KitApp.Instance.Progress.gold,Is.EqualTo(50));
        KitApp.Instance.Play(2);yield return Ready("VisualIngame");
        KitApp.Instance.Complete(true);
        Assert.That(KitApp.Instance.Progress.gold,Is.EqualTo(100));
        Assert.That(new LocalSaveStore(folder).Load().gold,Is.EqualTo(100));
    }

    [UnityTest]
    public IEnumerator SettingsControlSeparateChannelsAndSurviveSceneChanges()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        Binding("UIHUDPanel").Get<Button>("optionButton").onClick.Invoke();yield return null;
        var settings=Binding("Popup_Settings");
        Click(settings.Get<Toggle>("toggleMusic"));
        Assert.That(KitApp.Instance.Progress.musicEnabled,Is.False);
        Assert.That(KitApp.Instance.Progress.effectsEnabled,Is.True);
        Assert.That(settings.Get<Toggle>("toggleMusic").transform.Find("Background/Checkmark").gameObject.activeSelf,Is.True);
        Assert.That(settings.Get<Toggle>("toggleSound").transform.Find("Background/Checkmark").gameObject.activeSelf,Is.False);
        Click(settings.Get<Toggle>("toggleViberate"));
        Assert.That(KitApp.Instance.Progress.hapticsEnabled,Is.False);
        AssertChannels(false,true);
        var saved=new LocalSaveStore(folder).Load();
        Assert.That(saved.musicEnabled,Is.False);Assert.That(saved.effectsEnabled,Is.True);Assert.That(saved.hapticsEnabled,Is.False);
        yield return CheckFontLicenses(false);
        Dump("lobby-settings");
        settings.Get<Button>("btnClose").onClick.Invoke();yield return null;
        KitApp.Instance.Play(1);yield return Ready("VisualIngame");AssertChannels(false,true);
        Binding("UIIngamePanel").Get<Button>("IngameSettingButton").onClick.Invoke();yield return null;
        Assert.That(Time.timeScale,Is.Zero);
        settings=Binding("UIIngameSettingPopup");
        Assert.That(settings.Get<Toggle>("musicToggle").isOn,Is.False);
        Assert.That(settings.Get<Toggle>("hapticToggle").isOn,Is.False);
        Assert.That(settings.Get<Toggle>("musicToggle").transform.Find("Background/Checkmark").gameObject.activeSelf,Is.True);
        Click(settings.Get<Toggle>("sfxToggle"));AssertChannels(false,false);
        yield return CheckFontLicenses(true);
        Dump("ingame-settings");
        settings.Get<Button>("retryButton").onClick.Invoke();yield return Ready("VisualIngame");
        Assert.That(Time.timeScale,Is.EqualTo(1));AssertChannels(false,false);
    }
    [UnityTest]
    public IEnumerator CategorizedShopScrollsAndPreviewNeverCharges()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        Click(Binding("UIHUDPanel").Get<Toggle>("StoreToggle"));yield return new WaitForSecondsRealtime(.35f);
        var shop=UnityEngine.Object.FindFirstObjectByType<BoosterShopView>();
        Assert.That(shop.Scroll.vertical,Is.True);
        Assert.That(GameObject.Find("Category_No-Ad Offers"),Is.Not.Null);
        Assert.That(GameObject.Find("Category_Bundles"),Is.Not.Null);
        Assert.That(GameObject.Find("Category_Coins"),Is.Not.Null);
        Assert.That(shop.Scroll.content.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Preview_Bundle_")),Is.EqualTo(5));
        Assert.That(shop.Scroll.content.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Preview_Coins_")),Is.EqualTo(6));
        Dump("shop-top");
        Dump("shop-top-tall",1846);
        Assert.That(GameObject.Find("Shop canopy"),Is.Not.Null);
        Assert.That(GameObject.Find("UI_Top"),Is.Null,"Home HUD must be hidden on Shop");
        int balance=KitApp.Instance.Progress.gold;
        Click(GameObject.Find("Preview_NoAds_7").GetComponent<Button>());yield return null;
        Assert.That(UnityEngine.Object.FindFirstObjectByType<KitDialog>(),Is.Not.Null);
        Assert.That(KitApp.Instance.Progress.gold,Is.EqualTo(balance));
        Click(GameObject.Find("Dialog Close").GetComponent<Button>());yield return null;
        shop.Scroll.content.anchoredPosition=new Vector2(0,1300);Canvas.ForceUpdateCanvases();yield return null;
        Dump("shop-bundles");
        shop.Scroll.content.anchoredPosition=new Vector2(0,-shop.CoinsSection.anchoredPosition.y-82);Canvas.ForceUpdateCanvases();yield return null;
        Dump("shop-coins");
        Click(GameObject.Find("Preview_Coins_1000").GetComponent<Button>());yield return null;
        Assert.That(UnityEngine.Object.FindFirstObjectByType<KitDialog>(),Is.Not.Null);
        Assert.That(KitApp.Instance.Progress.gold,Is.EqualTo(balance));
        Click(GameObject.Find("Dialog Close").GetComponent<Button>());yield return null;
        shop.Scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();yield return null;
        Dump("shop-boosters");
        Click(Binding("UIHUDPanel").Get<Toggle>("HomeToggle"));yield return new WaitForSecondsRealtime(.35f);
        Assert.That(UnityEngine.Object.FindFirstObjectByType<LobbyNavigation>().SelectedPage,Is.EqualTo(1));
        Assert.That(GameObject.Find("Shop canopy"),Is.Null);
        Assert.That(GameObject.Find("UI_Top"),Is.Not.Null,"Home HUD must return after leaving Shop");
    }

    [UnityTest]
    public IEnumerator ShopPurchasesPersistAndBoostersConsumeOnlyWhenUsable()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        var saves=new LocalSaveStore(folder);saves.Save(new PlayerProgress {gold=3000});
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        var app=KitApp.Instance;
        Directory.CreateDirectory(Path.Combine(folder,"progress.json.tmp"));
        LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("BK_Kit: progress could not be saved"));
        Assert.That(app.TryBuyBooster(BoosterKind.ExtraBall,out _),Is.False);
        Assert.That(app.Progress.gold,Is.EqualTo(3000));Assert.That(app.Progress.Count(BoosterKind.ExtraBall),Is.Zero);
        Directory.Delete(Path.Combine(folder,"progress.json.tmp"));
        Click(Binding("UIHUDPanel").Get<Toggle>("StoreToggle"));yield return new WaitForSecondsRealtime(.35f);
        var shop=UnityEngine.Object.FindFirstObjectByType<BoosterShopView>();
        shop.Scroll.content.anchoredPosition=new Vector2(0,-shop.BoostersSection.anchoredPosition.y-82);
        Canvas.ForceUpdateCanvases();yield return null;
        Click(GameObject.Find("Buy_ExtraBall").GetComponent<Button>());
        Assert.That(app.Progress.gold,Is.EqualTo(2400));
        foreach(var kind in new[]{BoosterKind.Missile,BoosterKind.Bomb,BoosterKind.Laser})
            GameObject.Find("Buy_"+kind).GetComponent<Button>().onClick.Invoke();
        Assert.That(app.Progress.gold,Is.Zero);
        foreach(var offer in BoosterCatalog.All)Assert.That(app.Progress.Count(offer.Kind),Is.EqualTo(1));
        Assert.That(app.TryBuyBooster(BoosterKind.ExtraBall,out _),Is.False);
        Assert.That(saves.Load().extraBalls,Is.EqualTo(1));Dump("shop-stock");
        UnityEngine.Object.Destroy(app.gameObject);yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        app=KitApp.Instance;
        foreach(var offer in BoosterCatalog.All)Assert.That(app.Progress.Count(offer.Kind),Is.EqualTo(1));
        app.Play(1);yield return Ready("VisualIngame");
        var game=UnityEngine.Object.FindFirstObjectByType<BounceModule>();
        Directory.CreateDirectory(Path.Combine(folder,"progress.json.tmp"));
        LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("BK_Kit: progress could not be saved"));
        Assert.That(game.TryUseBooster(BoosterKind.ExtraBall),Is.False);
        Assert.That(app.Progress.extraBalls,Is.EqualTo(1));Assert.That(game.BallsRemaining,Is.EqualTo(20));
        Directory.Delete(Path.Combine(folder,"progress.json.tmp"));
        Time.timeScale=0;Assert.That(game.TryUseBooster(BoosterKind.ExtraBall),Is.False);Time.timeScale=1;
        Assert.That(app.Progress.extraBalls,Is.EqualTo(1));
        Click(Binding("UIIngamePanel").Get<Button>("ExtraBallIngameButton"));
        yield return BoosterFinished(game);
        Assert.That(game.BallsRemaining,Is.EqualTo(25));Assert.That(saves.Load().extraBalls,Is.Zero);
        Assert.That(game.TryUseBooster(BoosterKind.ExtraBall),Is.False);Assert.That(game.BallsRemaining,Is.EqualTo(25));
        int remaining=game.BlocksRemaining;
        Assert.That(game.TryUseBooster(BoosterKind.Missile),Is.True);yield return BoosterFinished(game);Assert.That(game.BlocksRemaining,Is.EqualTo(remaining-1));
        remaining=game.BlocksRemaining;Assert.That(game.TryUseBooster(BoosterKind.Bomb),Is.True);yield return BoosterFinished(game);Assert.That(game.BlocksRemaining,Is.LessThan(remaining));
        remaining=game.BlocksRemaining;Assert.That(game.TryUseBooster(BoosterKind.Laser),Is.True);yield return new WaitForSeconds(.25f);Dump("booster-laser");yield return BoosterFinished(game);Assert.That(game.BlocksRemaining,Is.LessThan(remaining));
        foreach(var offer in BoosterCatalog.All)Assert.That(saves.Load().Count(offer.Kind),Is.Zero);
        Dump("booster-effects");
        app.Play(1);yield return Ready("VisualIngame");
        Assert.That(UnityEngine.Object.FindFirstObjectByType<BounceModule>().BallsRemaining,Is.EqualTo(20));
        Assert.That(app.Progress.extraBalls,Is.Zero,"Restart must not refund used inventory");
    }

    [UnityTest]
    public IEnumerator BoosterClearsLastBlockAndAwardsOnce()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        new LocalSaveStore(folder).Save(new PlayerProgress {missiles=24,extraBalls=1});
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        KitApp.Instance.Play(1);yield return Ready("VisualIngame");
        var game=UnityEngine.Object.FindFirstObjectByType<BounceModule>();
        for(int i=0;i<24;i++){Assert.That(game.TryUseBooster(BoosterKind.Missile),Is.True);yield return BoosterFinished(game);}
        Assert.That(game.BlocksRemaining,Is.Zero);
        Assert.That(game.TryUseBooster(BoosterKind.ExtraBall),Is.False);
        yield return new WaitForSeconds(.7f);
        Assert.That(KitApp.Instance.Session.State,Is.EqualTo(SessionState.Won));
        var saved=new LocalSaveStore(folder).Load();
        Assert.That(saved.gold,Is.EqualTo(50));Assert.That(saved.unlockedLevel,Is.EqualTo(2));
        Assert.That(saved.missiles,Is.Zero);Assert.That(saved.extraBalls,Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator BoosterFlightPausesBlocksDuplicateInputAndCleansUpOnRetry()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        new LocalSaveStore(folder).Save(new PlayerProgress {missiles=2,bombs=1,extraBalls=1});
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        var app=KitApp.Instance;app.Play(1);yield return Ready("VisualIngame");
        var game=UnityEngine.Object.FindFirstObjectByType<BounceModule>();
        Assert.That(game.TryUseBooster(BoosterKind.Missile),Is.True);
        Assert.That(game.IsUsingBooster,Is.True);
        Assert.That(game.BlocksRemaining,Is.EqualTo(24),"Damage waits for impact");
        Assert.That(game.TryUseBooster(BoosterKind.Missile),Is.False);
        Assert.That(game.Fire(Vector3.forward),Is.False);
        Assert.That(app.Progress.missiles,Is.EqualTo(1));
        yield return new WaitForSeconds(.12f);
        Dump("booster-missile-flight");
        Time.timeScale=0;
        var projectile=GameObject.Find("Missile projectile").transform;
        var pausedPosition=projectile.position;
        yield return new WaitForSecondsRealtime(.5f);
        Assert.That(projectile.position,Is.EqualTo(pausedPosition));
        Assert.That(game.BlocksRemaining,Is.EqualTo(24));
        Time.timeScale=1;
        yield return new WaitForSeconds(.31f);
        Assert.That(UnityEngine.Object.FindObjectsByType<BlockBreakEffect>(FindObjectsSortMode.None).Length,Is.GreaterThan(0));
        foreach(var collider in UnityEngine.Object.FindObjectsByType<BlockBreakEffect>(FindObjectsSortMode.None).SelectMany(e=>e.GetComponentsInChildren<Collider>()))
            Assert.That(collider.enabled,Is.False,"Destruction visuals cannot reflect shots");
        Dump("booster-missile-impact");
        yield return BoosterFinished(game);
        Assert.That(game.BlocksRemaining,Is.EqualTo(23));
        Assert.That(UnityEngine.Object.FindObjectsByType<BoosterEffect>(FindObjectsSortMode.None),Is.Empty);
        Assert.That(game.TryUseBooster(BoosterKind.Bomb),Is.True);
        yield return new WaitForSeconds(.15f);Dump("booster-bomb-flight");
        app.Play(1);yield return Ready("VisualIngame");
        yield return new WaitForSeconds(.8f);
        game=UnityEngine.Object.FindFirstObjectByType<BounceModule>();
        Assert.That(game.BlocksRemaining,Is.EqualTo(24),"Cancelled old effects cannot damage a restarted board");
        Assert.That(game.IsUsingBooster,Is.False);
        Assert.That(UnityEngine.Object.FindObjectsByType<BoosterEffect>(FindObjectsSortMode.None),Is.Empty);
        Assert.That(new LocalSaveStore(folder).Load().bombs,Is.Zero,"Retry does not refund consumed stock");
        Assert.That(game.TryUseBooster(BoosterKind.ExtraBall),Is.True);
        yield return new WaitForSeconds(.15f);Dump("booster-extra-flight");
        yield return BoosterFinished(game);
        Assert.That(game.BallsRemaining,Is.EqualTo(25));
    }

    [UnityTest]
    public IEnumerator ZeroRewardAndInterruptedCollectionKeepSavedBalance()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        var saves=new LocalSaveStore(folder);saves.Save(new PlayerProgress {gold=int.MaxValue});
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        var app=KitApp.Instance;app.Play(1);yield return Ready("VisualIngame");app.Complete(true);
        yield return new WaitForSecondsRealtime(.25f);
        Assert.That(app.LastGoldReward,Is.Zero);
        var result=Binding("StageClearPopup");
        result.Get<Button>("closeButton").onClick.Invoke();
        yield return Ready("VisualLobby");
        Assert.That(saves.Load().gold,Is.EqualTo(int.MaxValue));
        Assert.That(UnityEngine.Object.FindFirstObjectByType<ClearRewardView>(),Is.Null);
        UnityEngine.Object.Destroy(app.gameObject);yield return null;
        saves.Save(new PlayerProgress {gold=25});
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        app=KitApp.Instance;app.Play(1);yield return Ready("VisualIngame");app.Complete(true);
        yield return new WaitForSecondsRealtime(.25f);
        Binding("StageClearPopup").Get<Button>("basic.claimButton").onClick.Invoke();
        yield return new WaitForSecondsRealtime(.2f);
        Assert.That(UnityEngine.Object.FindFirstObjectByType<ClearRewardView>().IsCollecting,Is.True);
        app.GoToLobby();yield return Ready("VisualLobby");
        app.Play(2);yield return Ready("VisualIngame");
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("VisualIngame"),"Destroyed collection cannot return a later game to lobby");
        Assert.That(saves.Load().gold,Is.EqualTo(75));
        Assert.That(GameObject.Find("Reward flight"),Is.Null);
    }

    [UnityTest]
    public IEnumerator OfflineProfileDialogsAndSaveRecoveryWork()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        var app=KitApp.Instance;
        Click(GameObject.Find("btn_profile").GetComponent<Button>());yield return null;
        var dialog=UnityEngine.Object.FindFirstObjectByType<KitDialog>();Assert.That(dialog,Is.Not.Null);
        var input=dialog.GetComponentInChildren<TMP_InputField>();input.text="Kit Tester";
        Click(dialog.GetComponentsInChildren<Button>().First(b=>b.name=="Save name"));
        Assert.That(new LocalSaveStore(folder).Load().playerName,Is.EqualTo("Kit Tester"));
        Assert.That(app.TrySetPlayerName("   "),Is.False);Assert.That(app.TrySetPlayerName(new string('a',25)),Is.False);
        Dump("local-profile");yield return null;
        Click(dialog.GetComponentsInChildren<Button>().First(b=>b.name=="Dialog Close"));yield return null;
        Assert.That(Time.timeScale,Is.EqualTo(1));
        var top=UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsSortMode.None).First(b=>b.role=="UIHUDSub_Top" && b.Get<VisualBindings>("heart")!=null);
        Assert.That(top.Get<VisualBindings>("heart").Get<TMP_Text>("fullText").text,Is.EqualTo("Free"));
        Click(top.Get<VisualBindings>("heart").Get<Button>("button"));yield return null;
        Assert.That(UnityEngine.Object.FindFirstObjectByType<KitDialog>(),Is.Not.Null);
        UnityEngine.Object.FindFirstObjectByType<KitDialog>().Close();yield return null;
        var offers=GameObject.Find("UI_Right");
        Click(offers.GetComponentsInChildren<Button>().First());yield return null;
        Assert.That(UnityEngine.Object.FindFirstObjectByType<KitDialog>(),Is.Not.Null,"Offline offer buttons respond without opening a service");
        UnityEngine.Object.FindFirstObjectByType<KitDialog>().Close();yield return null;
        app.Play(1);yield return Ready("VisualIngame");
        Directory.CreateDirectory(Path.Combine(folder,"progress.json.tmp"));
        LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("BK_Kit: progress could not be saved"));
        app.Complete(true);yield return null;
        Assert.That(app.HasPendingSave,Is.True);Assert.That(GameObject.Find("Save pending"),Is.Not.Null);
        Assert.That(app.Progress.gold,Is.EqualTo(50));
        app.Complete(true);Assert.That(app.Progress.gold,Is.EqualTo(50));
        Directory.Delete(Path.Combine(folder,"progress.json.tmp"));
        Assert.That(app.RetryPendingSave(),Is.True);yield return null;
        Assert.That(app.HasPendingSave,Is.False);Assert.That(GameObject.Find("Save pending"),Is.Null);
        Assert.That(new LocalSaveStore(folder).Load().gold,Is.EqualTo(50));
        app.GoToLobby();yield return Ready("VisualLobby");
        Assert.That(app.Progress.playerName,Is.EqualTo("Kit Tester"));
        Assert.That(app.TrySetSandboxProgress(7,500,3),Is.True);
        Assert.That(app.TrySetSandboxProgress(0,500,3),Is.False);
        app.Play(7);yield return Ready("VisualIngame");
        Assert.That(app.Session.Level,Is.EqualTo(7));
        Assert.That(new LocalSaveStore(folder).Load().bombs,Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator AllAuthoredBoardsAndLoopedStageLoadAndFire()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        new LocalSaveStore(folder).Save(new PlayerProgress {unlockedLevel=11});
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        for(int level=1;level<=11;level++)
        {
            KitApp.Instance.Play(level);yield return Ready("VisualIngame");
            var game=UnityEngine.Object.FindFirstObjectByType<BounceModule>();
            var boards=JsonUtility.FromJson<BounceModule.Levels>(game.presentation.levels.text).levels;
            var expected=boards[(level-1)%boards.Length];
            Assert.That(game.BallsRemaining,Is.EqualTo(expected.balls));
            Assert.That(game.BlocksRemaining,Is.EqualTo(expected.rows.Sum(r=>r.Count(c=>c>'0' && c<='9'))));
            Assert.That(game.ActiveBalls,Is.Zero);
            Assert.That(game.Fire(Vector3.forward),Is.True);
            yield return new WaitForSeconds(.12f);
            Assert.That(game.ActiveBalls,Is.EqualTo(1));
        }
    }

    private static IEnumerator BoosterFinished(BounceModule game)
    {
        float deadline=Time.realtimeSinceStartup+3;
        while(game.IsUsingBooster && Time.realtimeSinceStartup<deadline)yield return null;
        Assert.That(game.IsUsingBooster,Is.False,"Booster animation must finish");
        yield return null;
    }

    [UnityTest]
    public IEnumerator FailureRetryAndLobbyButtonsDoNotChargeGold()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        new LocalSaveStore(folder).Save(new PlayerProgress {gold=75});
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        KitApp.Instance.Play(1);yield return Ready("VisualIngame");KitApp.Instance.Complete(false);
        yield return new WaitForSeconds(.4f);
        var failure=Binding("UIFailPopup");
        Assert.That(failure.Get<TMP_Text>("continuePriceText").text,Is.EqualTo("Retry"));
        failure.Get<Button>("currencyButton").onClick.Invoke();Assert.That(KitApp.Instance.IsLoading,Is.False);
        Dump("failure");yield return null;Click(failure.Get<Button>("replayButton"));yield return Ready("VisualIngame");
        Assert.That(KitApp.Instance.Progress.gold,Is.EqualTo(75));Assert.That(KitApp.Instance.Session.State,Is.EqualTo(SessionState.Playing));
        Assert.That(UnityEngine.Object.FindFirstObjectByType<BounceModule>().BallsRemaining,Is.EqualTo(20));
        KitApp.Instance.Complete(false);yield return new WaitForSeconds(.3f);
        Click(Binding("UIFailPopup").Get<Button>("lobbyButton"));yield return Ready("VisualLobby");
        Assert.That(KitApp.Instance.Progress.gold,Is.EqualTo(75));Assert.That(KitApp.Instance.Progress.unlockedLevel,Is.EqualTo(1));
    }

    private static IEnumerator CheckFontLicenses(bool ingame)
    {
        Click(GameObject.Find("Font Licenses").GetComponent<Button>());yield return null;
        var dialog=UnityEngine.Object.FindFirstObjectByType<KitDialog>();
        Assert.That(dialog,Is.Not.Null);
        var scroll=dialog.GetComponentInChildren<ScrollRect>();
        var body=scroll.content.GetComponent<TMP_Text>();
        foreach(string family in new[]{"LilitaOne","Jua"})
            Assert.That(body.text,Does.Contain(Resources.Load<TextAsset>("FontLicenses/"+family+"/OFL").text));
        Assert.That(body.font.faceInfo.familyName,Does.Contain("Jua"));
        Assert.That(body.font.HasCharacters("한글 주아체 라이선스 출처 닫기 설정",out uint[] missing,true,true),Is.True);
        Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
        if(!ingame)Dump("font-licenses-top");
        scroll.verticalNormalizedPosition=0;yield return null;
        Assert.That(scroll.verticalNormalizedPosition,Is.EqualTo(0).Within(.01f));
        if(!ingame)Dump("font-licenses-bottom");
        Click(dialog.GetComponentsInChildren<Button>().First(b=>b.name=="Dialog Close"));yield return null;
        Assert.That(UnityEngine.Object.FindFirstObjectByType<KitDialog>(),Is.Null);
        Assert.That(Time.timeScale,Is.EqualTo(ingame?0:1));
    }

    private static VisualBindings Binding(string role)=>UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).First(b=>b.role==role);
    private static void AssertChannels(bool music,bool effects)
    {
        var channels=UnityEngine.Object.FindObjectsByType<KitAudioChannel>(FindObjectsSortMode.None);
        Assert.That(channels.Length,Is.GreaterThanOrEqualTo(2));
        foreach(var channel in channels)Assert.That(channel.IsMuted,Is.EqualTo(channel.Kind==KitAudioChannel.Category.Music?!music:!effects));
    }
    private static void Click(Selectable toggle)
    {
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)toggle.transform;
        var pointer=new PointerEventData(EventSystem.current) {position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.That(hits.Count,Is.GreaterThan(0),"No hit at "+pointer.position+" screen="+Screen.width+"x"+Screen.height+" target="+toggle.name+" raycast="+toggle.targetGraphic.raycastTarget);
        Assert.That(hits[0].gameObject.GetComponentInParent<Selectable>(),Is.EqualTo(toggle),"The selected tab must receive the actual UI raycast");
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static IEnumerator Ready(string name)
    {
        float limit=Time.realtimeSinceStartup+30;
        do {yield return null;Assert.That(Time.realtimeSinceStartup,Is.LessThan(limit));}
        while(KitApp.Instance==null || KitApp.Instance.IsLoading || SceneManager.GetActiveScene().name!=name);
        yield return null;yield return null;
        var scope=UnityEngine.Object.FindFirstObjectByType<IntegratedProjectScope>();
        Assert.That(scope,Is.Not.Null,"The integrated framework scope must own the flow");
        Assert.That(KitApp.Instance.UsesFrameworkServices,Is.True);
        Assert.That(scope.Container.Resolve<ISceneService>().ActiveScene.SceneName,Is.EqualTo(name));
        Assert.That(scope.Container.Resolve<IUIService>().Peek(UILayer.Content),Is.InstanceOf<ImportedSceneView>());
        Assert.That(UnityEngine.Object.FindObjectsByType<ImportedSceneView>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
        Assert.That(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
        Assert.That(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
    }
    private static void Dump(string name,int height=1600)
    {
        Directory.CreateDirectory("TestResults");
        var all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        File.WriteAllLines("TestResults/"+name+"-hierarchy.txt",all.Select(t=>
        {
            string path=t.name;for(var p=t.parent;p!=null;p=p.parent)path=p.name+"/"+path;
            return path+" active="+t.gameObject.activeInHierarchy+" pos="+t.localPosition+" scale="+t.localScale+" components="+string.Join(",",t.GetComponents<Component>().Select(c=>c==null?"MISSING":c.GetType().Name));
        }).OrderBy(s=>s));
        File.WriteAllLines("TestResults/"+name+"-materials.txt",UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials.Select(m=>r.name+" material="+(m==null?"NULL":m.name+" shader="+m.shader.name+" supported="+m.shader.isSupported))));
        var camera=Camera.main;
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
        var target=new RenderTexture(900,height,24);
        camera.targetTexture=target;Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(900,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,height),0,0);image.Apply();
        File.WriteAllBytes("TestResults/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;
        foreach(var canvas in canvases)canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        UnityEngine.Object.Destroy(image);target.Release();UnityEngine.Object.Destroy(target);
    }
    [UnityTearDown]
    public IEnumerator Clean()
    {
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        var scope=UnityEngine.Object.FindFirstObjectByType<IntegratedProjectScope>();
        if(scope!=null)UnityEngine.Object.Destroy(scope.gameObject);
        yield return null;
        foreach(var root in UnityEngine.Object.FindObjectsByType<UIRoot>(FindObjectsSortMode.None))UnityEngine.Object.Destroy(root.gameObject);
        yield return null;
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",null);
        if(folder!=null && Directory.Exists(folder))Directory.Delete(folder,true);
        Time.timeScale=1;
    }
}
