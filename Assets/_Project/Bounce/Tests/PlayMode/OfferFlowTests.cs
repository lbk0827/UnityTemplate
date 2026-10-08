using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BK.Kit;
using BK.Meta;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class OfferFlowTests
{
    private string folder;

    [UnityTest]
    public IEnumerator WeeklyStepOfferShowsOneIconAndFreeStepsGrantLocalRewards()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        KitTestSaves.Seed(folder,level:new BounceStepOffers().UnlockStage);
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");
        yield return Ready("VisualLobby");
        var app=KitApp.Instance;
        var offers=UnityEngine.Object.FindFirstObjectByType<LobbyOffersView>();
        Assert.That(offers,Is.Not.Null,"Lobby must own a LobbyOffersView");
        var campaign=app.Offers.GetActive();
        Assert.That(campaign.HasValue,Is.True,"The epoch is in the past, so some week is always live");
        bool vertical=campaign.Value.Definition.Type==StepOfferType.Vertical;
        var type=campaign.Value.Definition.Type;
        string activeName=vertical?"Lobby_EndlessOffer":"Lobby_EndlessGift",otherName=vertical?"Lobby_EndlessGift":"Lobby_EndlessOffer";
        int firstReward=vertical?100:50;int paidProduct=vertical?1001:1011;
        var right=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="UI_Right");
        var icon=right.GetComponentsInChildren<Button>(true).First(b=>HierarchyPath(b.transform).Contains(activeName));
        var other=right.GetComponentsInChildren<Button>(true).First(b=>HierarchyPath(b.transform).Contains(otherName));
        Assert.That(icon.gameObject.activeInHierarchy,Is.True,"The week's type is shown");
        Assert.That(other.gameObject.activeInHierarchy,Is.False,"Only one step offer type is live per week (sf)");
        var remain=right.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="TXT_Remain" && HierarchyPath(t.transform).Contains(activeName));
        if(remain!=null)Assert.That(remain.text,Does.Match(@"^\d+d \d+h$|^\d+h \d+m$|^\d+m \d+s$"),"Remaining time uses the sf format, got "+remain.text);
        var dot=right.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="IMG_RedDot" && HierarchyPath(t).Contains(activeName));
        if(dot!=null)Assert.That(dot.gameObject.activeInHierarchy,Is.True,"First step is free, so the icon shows a red dot");
        Assert.That(UnityEngine.Object.FindFirstObjectByType<KitDialog>(),Is.Null);
        Click(icon);
        yield return null;
        Assert.That(offers.IsOpen,Is.True,"Clicking the lobby icon opens the imported popup");
        Assert.That(UnityEngine.Object.FindFirstObjectByType<KitDialog>(),Is.Null,"No offline notice dialog");
        string role=vertical?"UIEndlessOfferPopup":"UIEndlessGiftsPopup";
        var binding=offers.Popup.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role==role);
        Assert.That(binding.gameObject.activeInHierarchy,Is.True);
        var slot0=binding.Get<VisualBindings>("slots.0");var slot1=binding.Get<VisualBindings>("slots.1");
        Assert.That(slot0,Is.Not.Null);Assert.That(slot1,Is.Not.Null);
        Assert.That(slot0.Get<GameObject>("currentRoot").activeSelf,Is.True);
        Assert.That(slot1.Get<GameObject>("lockRoot").activeSelf,Is.True);
        Assert.That(slot0.Get<TMP_Text>("priceText").text,Is.EqualTo("FREE"));
        string paidPrice=ShopCatalog.Price(app.Shop.Products.Get(paidProduct));
        Assert.That(slot1.Get<TMP_Text>("priceText").text,Is.EqualTo(paidPrice),"Paid steps show the linked shop product's price");
        Assert.That(slot0.Get<Transform>("rewardContainer").Cast<Transform>().Count(t=>t.name=="Reward clone" && t.gameObject.activeSelf),Is.EqualTo(1));
        Assert.That(slot1.Get<Transform>("rewardContainer").Cast<Transform>().Count(t=>t.name=="Reward clone" && t.gameObject.activeSelf),Is.EqualTo(app.Shop.Products.Get(paidProduct).rewards.Length),"Paid steps list the product contents");
        Assert.That(slot0.transform.lossyScale.x,Is.GreaterThan(0.1f),"Authored Target node must be scaled open");
        var canvasRect=(RectTransform)offers.Popup.transform.parent;
        Assert.That(OnScreen(canvasRect,binding.Get<Button>("closeButton")),Is.True,"Close button must sit inside the canvas");
        Assert.That(OnScreen(canvasRect,slot0.Get<Button>("buyButton")),Is.True,"Current slot's button must be on screen");
        yield return null;
        Dump("offer-active");
        long goldBefore=app.Gold;
        var buy=slot0.Get<Button>("buyButton");
        Assert.That(buy.interactable,Is.True);
        buy.onClick.Invoke();
        yield return null;
        Assert.That(app.Gold,Is.EqualTo(goldBefore+firstReward),"Free step grants local gold");
        Assert.That(KitTestSaves.Gold(folder),Is.EqualTo(goldBefore+firstReward));
        Assert.That(KitTestSaves.StepOfferNextStep(folder,type),Is.EqualTo(2),"Progress is saved through StepOfferData");
        Assert.That(offers.IsOpen,Is.True,"Claiming a step keeps the popup open");
        // sf: past steps disappear; the paid step 2 is now the first visible card.
        Assert.That(slot0.Get<GameObject>("currentRoot").activeSelf,Is.True);
        Assert.That(slot0.Get<TMP_Text>("priceText").text,Is.EqualTo(paidPrice));
        Assert.That(slot0.Get<Button>("buyButton").interactable,Is.True);
        Assert.That(slot1.Get<GameObject>("lockRoot").activeSelf,Is.True);
        Assert.That(slot1.Get<TMP_Text>("priceText").text,Is.EqualTo("FREE"),"Step 3 is free again");
        slot0.Get<Button>("buyButton").onClick.Invoke();
        yield return null;
        Assert.That(app.Gold,Is.EqualTo(goldBefore+firstReward),"Paid step grants nothing offline");
        Assert.That(KitTestSaves.StepOfferNextStep(folder,type),Is.EqualTo(2),"Paid step does not advance without a store");
        var notice=UnityEngine.Object.FindFirstObjectByType<KitDialog>();
        Assert.That(notice,Is.Not.Null,"Paid step explains that payments are not connected");
        notice.Close();yield return null;
        Dump("offer-paid-step");
        var close=binding.Get<Button>("closeButton");
        Assert.That(close,Is.Not.Null);
        close.onClick.Invoke();
        yield return null;
        Assert.That(offers.IsOpen,Is.False);
        Assert.That(UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(b=>b.role==role),Is.False,"Popup instance is destroyed");
        if(dot!=null)Assert.That(dot.gameObject.activeInHierarchy,Is.False,"Current step is paid, so the red dot hides");
        Assert.That(icon.gameObject.activeInHierarchy,Is.True,"The icon stays until the week ends or the ladder completes");

        // Welcome deal: visuals and local reward list, purchase only informs.
        var welcome=right.GetComponentsInChildren<Button>(true).First(b=>HierarchyPath(b.transform).Contains("Welcome"));
        Click(welcome);
        yield return null;
        Assert.That(offers.IsOpen,Is.True);Assert.That(offers.OpenKind,Is.EqualTo(LobbyOfferIcon.WelcomeDeal));
        var deal=offers.Popup.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="UIWelcomeDealPopup");
        Assert.That(deal.Get<TMP_Text>("priceText").text,Is.EqualTo(LobbyOffersView.WelcomePrice));
        Assert.That(deal.Get<Transform>("rewardContainer").GetComponentsInChildren<VisualBindings>(false).Count(b=>b.role=="UIWelcomeDealRewardItem"),Is.EqualTo(LobbyOffersView.WelcomeRewards.Length));
        yield return null;
        Dump("offer-welcome");
        deal.Get<Button>("buyButton").onClick.Invoke();
        yield return null;
        Assert.That(app.Gold,Is.EqualTo(goldBefore+firstReward));
        notice=UnityEngine.Object.FindFirstObjectByType<KitDialog>();
        Assert.That(notice,Is.Not.Null);notice.Close();yield return null;
        deal.Get<Button>("dimButton").onClick.Invoke();
        yield return null;
        Assert.That(offers.IsOpen,Is.False);
        var stage=UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.role=="StageInfo");
        Assert.That(stage.Get<Button>("buttons.0.button").interactable,Is.True);
    }

    [UnityTest]
    public IEnumerator StepOffersStayHiddenBelowTheUnlockStage()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");
        yield return Ready("VisualLobby");
        Assert.That(KitApp.Instance.Offers.GetActive().HasValue,Is.False,"Stage 1 is below the unlock stage");
        var right=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="UI_Right");
        foreach(var button in right.GetComponentsInChildren<Button>(true))
            if(HierarchyPath(button.transform).Contains("Endless"))Assert.That(button.gameObject.activeInHierarchy,Is.False,HierarchyPath(button.transform));
        var offers=UnityEngine.Object.FindFirstObjectByType<LobbyOffersView>();
        offers.Open(LobbyOfferIcon.EndlessOffer);offers.Open(LobbyOfferIcon.EndlessGift);
        Assert.That(offers.IsOpen,Is.False,"Locked offers cannot be opened by code either");
    }

    private static bool OnScreen(RectTransform canvas,Component target)
    {
        var rect=(RectTransform)target.transform;
        return canvas.rect.Contains(canvas.InverseTransformPoint(rect.TransformPoint(rect.rect.center)));
    }
    private static string HierarchyPath(Transform t){string path=t.name;for(var p=t.parent;p!=null;p=p.parent)path=p.name+"/"+path;return path;}
    private static void Click(Selectable target)
    {
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)target.transform;
        var pointer=new PointerEventData(EventSystem.current) {position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.That(hits.Count,Is.GreaterThan(0),"No hit at "+pointer.position+" target="+HierarchyPath(target.transform));
        Assert.That(hits[0].gameObject.GetComponentInParent<Selectable>(),Is.EqualTo(target),"The offer icon must receive the actual UI raycast; hit "+HierarchyPath(hits[0].gameObject.transform));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static IEnumerator Ready(string name)
    {
        float limit=Time.realtimeSinceStartup+30;
        do {yield return null;Assert.That(Time.realtimeSinceStartup,Is.LessThan(limit));}
        while(KitApp.Instance==null || KitApp.Instance.IsLoading || SceneManager.GetActiveScene().name!=name);
        yield return null;yield return null;
    }
    private static void Dump(string name)
    {
        Directory.CreateDirectory("TestResults");
        var camera=Camera.main;
        var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
        var target=new RenderTexture(900,1600,24);
        camera.targetTexture=target;Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(900,1600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,900,1600),0,0);image.Apply();
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
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",null);
        if(folder!=null && Directory.Exists(folder))Directory.Delete(folder,true);
        Time.timeScale=1;
    }
}
