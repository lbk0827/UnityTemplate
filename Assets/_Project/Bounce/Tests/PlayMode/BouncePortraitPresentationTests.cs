using System;
using System.Collections;
using System.IO;
using System.Linq;
using BK.Kit;
using BK.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public sealed class BouncePortraitPresentationTests
{
    private string folder,previous;
    [UnityTest,Timeout(180000)]
    public IEnumerator PortraitBoardUsesScreenAndHudStaysBelowLauncher()
    {
        previous=Environment.GetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR");
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)Object.Destroy(KitApp.Instance.gameObject);yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        var app=KitApp.Instance;
        foreach(var type in BounceReleasePolicy.Families)app.MarkGuideSeen("gimmick."+type);
        foreach(var size in new[]{new Vector2Int(600,1066),new Vector2Int(600,1333),new Vector2Int(768,1024)})
        foreach(int stage in new[]{1,13,14,140})
        {
            app.TrySetSandboxProgress(stage,1000,0);app.Play(stage);yield return Ready("VisualIngame");
            var game=Object.FindFirstObjectByType<BounceModule>();var camera=Camera.main;
            camera.aspect=(float)size.x/size.y;game.Begin(stage,_=>{});yield return null;
            var level=JsonUtility.FromJson<BounceModule.Levels>(game.presentation.levels.text).Resolve(stage);
            float upper=camera.WorldToViewportPoint(new Vector3(0,1.8f,BounceBoardLayout.Rows-3.9f)).y;
            float lower=camera.WorldToViewportPoint(new Vector3(0,0,-10.4f)).y;
            Assert.That(upper,Is.LessThanOrEqualTo(.965f));Assert.That(lower,Is.GreaterThanOrEqualTo(.08f));
            Assert.That(camera.WorldToViewportPoint(new Vector3(6.5f,1,0)).x,Is.LessThan(1));
            Assert.That(camera.orthographicSize,Is.EqualTo(Mathf.Max(11.75f,6.6f/camera.aspect)).Within(.001f));
            Assert.That(GameObject.Find("Wall left").transform.position,Is.EqualTo(new Vector3(-6,.6f,2.75f)));
            Assert.That(GameObject.Find("Wall left").transform.localScale,Is.EqualTo(new Vector3(1,1.2f,17.5f)));
            Assert.That(GameObject.Find("Wall top").transform.position.z,Is.EqualTo(11));
            foreach(var t in GameObject.Find("Bounce_Rig").GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Block_")))
            {
                var cell=t.name.Split('_');
                Assert.That(t.position,Is.EqualTo(BounceGimmicks.Cell(int.Parse(cell[1]),int.Parse(cell[2]),level.rows[0].Length,16)));
            }
            if(size.x<size.y*.65f)Assert.That(upper-lower,Is.GreaterThan(.65f),"Portrait play area must occupy the screen");
            foreach(var offer in BoosterCatalog.All)
            {
                var rect=(RectTransform)GameObject.Find("BTN_"+offer.Kind).transform;
                Assert.That(rect.anchorMin.y,Is.EqualTo(.045f));
            }
            Capture(camera,size,"stage-"+stage.ToString("000")+"-"+size.x+"x"+size.y);
        }
    }
    private static IEnumerator Ready(string scene)
    {
        float end=Time.realtimeSinceStartup+30;
        while(KitApp.Instance==null || KitApp.Instance.IsLoading || SceneManager.GetActiveScene().name!=scene)
        {Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}
        yield return null;
    }
    private static void Capture(Camera camera,Vector2Int size,string name)
    {
        Directory.CreateDirectory("TestResults/PortraitGarden");
        var target=new RenderTexture(size.x,size.y,24){antiAliasing=4};target.Create();
        var previous=RenderTexture.active;var old=camera.targetTexture;
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        try
        {
            foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
            RenderTexture.active=target;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes("TestResults/PortraitGarden/"+name+".png",image.EncodeToPNG());Object.Destroy(image);
        }
        finally {foreach(var c in canvases)c.renderMode=RenderMode.ScreenSpaceOverlay;camera.targetTexture=old;RenderTexture.active=previous;target.Release();Object.Destroy(target);}
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        Time.timeScale=1;
        if(KitApp.Instance!=null)Object.Destroy(KitApp.Instance.gameObject);yield return null;
        var scope=Object.FindFirstObjectByType<IntegratedProjectScope>();if(scope!=null)Object.Destroy(scope.gameObject);yield return null;
        foreach(var root in Object.FindObjectsByType<UIRoot>(FindObjectsSortMode.None))Object.Destroy(root.gameObject);yield return null;
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",previous);
        if(folder!=null && Directory.Exists(folder))Directory.Delete(folder,true);
    }
}
