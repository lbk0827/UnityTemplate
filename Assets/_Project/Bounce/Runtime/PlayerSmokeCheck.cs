#if DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BK.Kit
{
    // Opt-in built-player check with an isolated profile. Never runs during ordinary gameplay.
    public sealed class PlayerSmokeCheck : MonoBehaviour
    {
        private bool failed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartCheck()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--bk-smoke")<0)return;
            if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR")))
            {Debug.LogError("Smoke check requires an isolated BK_KIT_TEST_SAVE_DIR.");Application.Quit(2);return;}
            var go=new GameObject("Player smoke check");DontDestroyOnLoad(go);go.AddComponent<PlayerSmokeCheck>();
        }
        private IEnumerator Start()
        {
            yield return Scene("VisualLobby");if(failed)yield break;
            var app=KitApp.Instance;
            if(!Check(app.UsesFrameworkServices,"Framework scene/UI integration"))yield break;
            if(!Check(app.TrySetSandboxProgress(1,350,1),"Seed test profile"))yield break;
            app.Play(1);yield return Scene("VisualIngame");if(failed)yield break;
            var game=FindFirstObjectByType<BounceModule>();
            if(!Check(game!=null && game.BlocksRemaining==24 && game.BallsRemaining==20,"Load board"))yield break;
            if(!Check(game.TryUseBooster(BoosterKind.ExtraBall),"Use booster"))yield break;
            float deadline=Time.realtimeSinceStartup+4;
            while(game.IsUsingBooster && Time.realtimeSinceStartup<deadline)yield return null;
            if(!Check(!game.IsUsingBooster && game.BallsRemaining==25,"Finish booster"))yield break;
            if(!Check(game.Fire(Vector3.forward),"Fire ball"))yield break;
            yield return new WaitForSeconds(1);
            if(!Check(game.BlocksRemaining<24,"Physical block hit"))yield break;
            app.Complete(true);yield return new WaitForSeconds(.4f);
            var reward=FindFirstObjectByType<ClearRewardView>();
            if(!Check(reward!=null && app.Gold==400,"Saved clear reward"))yield break;
            reward.Collect();yield return Scene("VisualLobby");if(failed)yield break;
            yield return new WaitForSecondsRealtime(1);
            if(!Check(app.UnlockedLevel==2 && app.BoosterCount(BoosterKind.ExtraBall)==0,"Progress and inventory"))yield break;
            string folder=Environment.GetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR");
            if(!Check(new BK.Save.SaveService(folder).Get<BK.Meta.WalletData>().Find("Gold").value==400,"Persisted reward"))yield break;
            if(!Check(CaptureLobby(Path.Combine(folder,"smoke-lobby.png")),"Rendered lobby contains visible UI"))yield break;
            yield return new WaitForSecondsRealtime(.5f);
            Debug.Log("BK_KIT_PLAYER_SMOKE_OK");Application.Quit(0);
        }
        private static bool CaptureLobby(string path)
        {
            // Hidden validation windows may not present a backbuffer. Render explicitly offscreen.
            var camera=Camera.main;
            var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(c=>c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            var target=new RenderTexture(900,1600,24);
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            var image=new Texture2D(900,1600,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,900,1600),0,0);image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
            var pixels=image.GetPixels32();
            bool visible=pixels.Count(p=>p.r>20 || p.g>20 || p.b>20)>pixels.Length/4;
            RenderTexture.active=previous;camera.targetTexture=null;
            foreach(var canvas in canvases)canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            Destroy(image);target.Release();Destroy(target);
            return visible;
        }
        private IEnumerator Scene(string expected)
        {
            float deadline=Time.realtimeSinceStartup+15;
            while(Time.realtimeSinceStartup<deadline)
            {
                if(KitApp.Instance!=null && !KitApp.Instance.IsLoading && SceneManager.GetActiveScene().name==expected)
                {yield return null;yield break;}
                yield return null;
            }
            Check(false,"Scene timeout: "+expected);
        }
        private bool Check(bool condition,string message)
        {
            if(condition)return true;
            failed=true;Debug.LogError("BK_KIT_PLAYER_SMOKE_FAILED: "+message);Application.Quit(2);return false;
        }
    }
}
#endif
