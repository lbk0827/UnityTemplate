using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BK.Kit;
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
    public IEnumerator LobbyIconsOpenImportedOfferPopupsAndFreeStepsGrantLocalRewards()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");
        yield return Ready("VisualLobby");
        var app=KitApp.Instance;
        var offers=UnityEngine.Object.FindFirstObjectByType<LobbyOffersView>();
        Assert.That(offers,Is.Not.Null,"Lobby must own a LobbyOffersView");
        var right=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="UI_Right");
        var icon=right.GetComponentsInChildren<Button>(true).First(b=>HierarchyPath(b.transform).Contains("Lobby_EndlessOffer"));
        var dot=right.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="IMG_RedDot" && HierarchyPath(t).Contains("Lobby_EndlessOffer"));
        if(dot!=null)Assert.That(dot.gameObject.activeInHierarchy,Is.True,"First step is free, so the icon shows a red dot");
        Assert.That(UnityEngine.Object.FindFirstObjectByType<KitDialog>(),Is.Null);
        Click(icon);
        yield return null;
        Assert.That(offers.IsOpen,Is.True,"Clicking the lobby icon opens the imported popup instead of an offline notice");
        Assert.That(UnityEngine.Object.FindFirstObjectByType<KitDialog>(),Is.Null,"No offline notice dialog");
        var binding=offers.Popup.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="UIEndlessOfferPopup");
        Assert.That(binding.gameObject.activeInHierarchy,Is.True);
        var slot0=binding.Get<VisualBindings>("slots.0");var slot1=binding.Get<VisualBindings>("slots.1");
        Assert.That(slot0,Is.Not.Null);Assert.That(slot1,Is.Not.Null);
        Assert.That(slot0.Get<GameObject>("currentRoot").activeSelf,Is.True);
        Assert.That(slot1.Get<GameObject>("lockRoot").activeSelf,Is.True);
        Assert.That(slot0.Get<TMP_Text>("priceText").text,Is.EqualTo("FREE"));
        Assert.That(slot1.Get<TMP_Text>("priceText").text,Is.EqualTo(OfferCatalog.EndlessOffer[1].Price));
        Assert.That(slot0.Get<Transform>("rewardContainer").Cast<Transform>().Count(t=>t.name=="Reward clone" && t.gameObject.activeSelf),Is.EqualTo(OfferCatalog.EndlessOffer[0].Rewards.Count));
        Assert.That(slot0.transform.lossyScale.x,Is.GreaterThan(0.1f),"Authored Target node must be scaled open");
        var canvasRect=(RectTransform)offers.Popup.transform.parent;
        var closeRect=(RectTransform)binding.Get<Button>("closeButton").transform;
        var closeCenter=canvasRect.InverseTransformPoint(closeRect.TransformPoint(closeRect.rect.center));
        Assert.That(canvasRect.rect.Contains(closeCenter),Is.True,"Close button must sit inside the canvas, got "+closeCenter+" in "+canvasRect.rect);
        Assert.That(OnScreen(canvasRect,slot0.Get<Button>("buyButton")),Is.True,"Current slot's button must be on screen");
        yield return null;
        Dump("offer-endless");
        long goldBefore=app.Gold;
        var buy=slot0.Get<Button>("buyButton");
        Assert.That(buy.interactable,Is.True);
        buy.onClick.Invoke();
        yield return null;
        Assert.That(app.Gold,Is.EqualTo(goldBefore+100),"Free step grants local gold");
        Assert.That(app.OfferStep(OfferKind.EndlessOffer),Is.EqualTo(1));
        Assert.That(KitTestSaves.Gold(folder),Is.EqualTo(goldBefore+100));Assert.That(KitTestSaves.OfferStep(folder,OfferKind.EndlessOffer),Is.EqualTo(1));
        Assert.That(slot0.Get<GameObject>("checkRoot").activeSelf,Is.True,"Claimed step shows the check stamp");
        Assert.That(slot0.Get<Button>("buyButton").interactable,Is.False);
        Assert.That(slot1.Get<GameObject>("currentRoot").activeSelf,Is.True);
        Assert.That(slot1.Get<Button>("buyButton").interactable,Is.True);
        slot1.Get<Button>("buyButton").onClick.Invoke();
        yield return null;
        Assert.That(app.Gold,Is.EqualTo(goldBefore+100),"Paid step grants nothing offline");
        var notice=UnityEngine.Object.FindFirstObjectByType<KitDialog>();
        Assert.That(notice,Is.Not.Null,"Paid step explains that payments are not connected");
        notice.Close();yield return null;
        var close=binding.Get<Button>("closeButton");
        Assert.That(close,Is.Not.Null);
        close.onClick.Invoke();
        yield return null;
        Assert.That(offers.IsOpen,Is.False);
        Assert.That(UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsInactive.Include,FindObjectsSortMode.None).Any(b=>b.role=="UIEndlessOfferPopup"),Is.False,"Popup instance is destroyed");
        if(dot!=null)Assert.That(dot.gameObject.activeInHierarchy,Is.False,"Current step is paid, so the red dot hides");

        // Later steps live below the screen in the authored rail; opening must bring the current step into view.
        app.SetOfferStep(OfferKind.EndlessOffer,OfferCatalog.EndlessOffer.Count-1);
        offers.Open(OfferKind.EndlessOffer);
        yield return null;
        binding=offers.Popup.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="UIEndlessOfferPopup");
        var lastSlot=binding.Get<VisualBindings>("slots."+(OfferCatalog.EndlessOffer.Count-1));
        Assert.That(lastSlot.Get<GameObject>("currentRoot").activeSelf,Is.True);
        Assert.That(OnScreen(canvasRect,lastSlot.Get<Button>("buyButton")),Is.True,"Rail must move so the current (last) step is visible");
        Assert.That(OnScreen(canvasRect,binding.Get<Button>("closeButton")),Is.True,"Close button stays on screen while the rail moves");
        Assert.That(binding.Get<VisualBindings>("slots.0").Get<GameObject>("root").activeSelf,Is.False,"Slots pushed above the content area are switched off, keeping the title clear");
        yield return null;
        Dump("offer-endless-last");
        offers.Close();yield return null;
        app.SetOfferStep(OfferKind.EndlessOffer,1);

        // Welcome deal: visuals and local reward list, purchase only informs.
        var welcome=right.GetComponentsInChildren<Button>(true).First(b=>HierarchyPath(b.transform).Contains("Welcome"));
        Click(welcome);
        yield return null;
        Assert.That(offers.IsOpen,Is.True);Assert.That(offers.OpenKind,Is.EqualTo(OfferKind.WelcomeDeal));
        var deal=offers.Popup.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="UIWelcomeDealPopup");
        Assert.That(deal.Get<TMP_Text>("priceText").text,Is.EqualTo(OfferCatalog.WelcomeDeal.Price));
        Assert.That(deal.Get<Transform>("rewardContainer").GetComponentsInChildren<VisualBindings>(false).Count(b=>b.role=="UIWelcomeDealRewardItem"),Is.EqualTo(OfferCatalog.WelcomeDeal.Rewards.Count));
        yield return null;
        Dump("offer-welcome");
        deal.Get<Button>("buyButton").onClick.Invoke();
        yield return null;
        Assert.That(app.Gold,Is.EqualTo(goldBefore+100));
        notice=UnityEngine.Object.FindFirstObjectByType<KitDialog>();
        Assert.That(notice,Is.Not.Null);notice.Close();yield return null;
        deal.Get<Button>("dimButton").onClick.Invoke();
        yield return null;
        Assert.That(offers.IsOpen,Is.False);

        // Gifts popup opens too and the stage path still works after closing.
        var gift=right.GetComponentsInChildren<Button>(true).First(b=>HierarchyPath(b.transform).Contains("Lobby_EndlessGift"));
        Click(gift);yield return null;
        Assert.That(offers.OpenKind,Is.EqualTo(OfferKind.EndlessGift));
        var gifts=offers.Popup.GetComponentsInChildren<VisualBindings>(true).First(b=>b.role=="UIEndlessGiftsPopup");
        Assert.That(gifts.Get<VisualBindings>("slots.6"),Is.Not.Null);
        yield return null;
        Dump("offer-gifts");
        offers.Close();yield return null;
        var stage=UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.role=="StageInfo");
        Assert.That(stage.Get<Button>("buttons.0.button").interactable,Is.True);
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
