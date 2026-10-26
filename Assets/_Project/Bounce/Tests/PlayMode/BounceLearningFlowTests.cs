using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BK.Kit;
using BK.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public sealed class BounceLearningFlowTests
{
    [Serializable] public sealed class ShotRecord { public float angle; public int before, after; }
    [Serializable] public sealed class BoardRecord { public int stage, budget, objectives; public ShotRecord[] shots; public bool replayPassed; }
    [Serializable] public sealed class Report { public string kind = "actual-runtime-solvability-not-cohort-win-rate"; public BoardRecord[] boards; }
    private string folder;
    private float previousScale;
    private BounceModule game;
    private static readonly Vector3 Origin = new Vector3(0,.5f,-8.5f);
    private static readonly MethodInfo Reflect = typeof(BounceModule).GetMethod("Reflect", BindingFlags.Static | BindingFlags.NonPublic);

    [UnityTest]
    public IEnumerator FirstMineGuideShowsTheMineRuleAndPreservesTheBallBudget()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);previousScale=Time.timeScale;
        if(KitApp.Instance!=null)Object.Destroy(KitApp.Instance.gameObject);yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        var app=KitApp.Instance;Assert.That(app.TrySetSandboxProgress(6,1000,0),Is.True);
        app.Play(6);yield return Ready("VisualIngame");yield return new WaitForSecondsRealtime(.4f);
        game=Object.FindFirstObjectByType<BounceModule>();
        var popup=Object.FindFirstObjectByType<CommonPopupView>();
        Assert.That(popup,Is.Not.Null);Assert.That(Time.timeScale,Is.Zero);
        var text=string.Join("\n",popup.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text));
        Assert.That(text,Does.Contain("Sea mine"));Assert.That(text,Does.Contain("eight surrounding cells"));
        Assert.That(text,Does.Not.Contain("Moving panel"));
        var hero=popup.GetComponentsInChildren<UnityEngine.UI.Image>().Single(i=>i.name=="IMG_Gimmick");
        Assert.That(hero.enabled,Is.True);Assert.That(hero.sprite,Is.Not.Null);
        Assert.That(hero.sprite.name,Is.EqualTo("Guide_mine"),"Show the actual mine, not the furnace illustration");
        int balls=game.BallsRemaining;
        Directory.CreateDirectory("TestResults/BounceLearning");Capture(6,"guide-mine.png");
        popup.Close();yield return new WaitForSecondsRealtime(.4f);
        Assert.That(app.HasSeenGuide("gimmick.mine"),Is.True);
        Assert.That(Time.timeScale,Is.EqualTo(1));Assert.That(game.BallsRemaining,Is.EqualTo(balls));
    }

    [UnityTest, Timeout(1200000)]
    public IEnumerator TenProductionLearningBoardsClearAndReplayWithoutBoosters()
    {
        folder = Path.Combine(Path.GetTempPath(), "BKKitTests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR", folder);
        previousScale = Time.timeScale;
        if (KitApp.Instance != null) Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap"); yield return Ready("VisualLobby");
        var app = KitApp.Instance;
        app.MarkGuideSeen("gimmick.mine");
        Directory.CreateDirectory("TestResults/BounceLearning");
        var records = new List<BoardRecord>();
        for (int stage = 1; stage <= 10; stage++)
        {
            Time.timeScale = 1;
            Assert.That(app.TrySetSandboxProgress(stage, 1000, 0), Is.True);
            app.Play(stage); yield return Ready("VisualIngame");
            game = Object.FindFirstObjectByType<BounceModule>();
            Assert.That(game.presentation.levels.name, Is.EqualTo("bounce-learning-levels"), "Test the shipping assignment, not the legacy fixtures");
            Camera.main.aspect=9f/16;
            game.Begin(stage, _ => {}); yield return null;
            var authored=JsonUtility.FromJson<BounceModule.Levels>(game.presentation.levels.text).levels[stage-1];
            Assert.That(GameObject.Find("Wall top").transform.position.z,Is.EqualTo(authored.GridRows-5));
            foreach(Transform block in GameObject.Find("Bounce_Rig").transform)
            {
                if(!block.name.StartsWith("Block_"))continue;
                var cell=block.name.Split('_');
                Assert.That(block.position,Is.EqualTo(BounceGimmicks.Cell(int.Parse(cell[1]),int.Parse(cell[2]),authored.rows[0].Length,authored.GridRows)));
            }
            Capture(stage);
            var record = new BoardRecord { stage=stage, budget=game.BallsRemaining, objectives=game.BlocksRemaining };
            var shots = new List<ShotRecord>();
            var missed = new HashSet<int>();
            while (game.BlocksRemaining > 0 && game.BallsRemaining > 0)
            {
                float angle = FindVisibleShot(missed);
                int before = game.BlocksRemaining;
                yield return FireAndSettle(angle);
                shots.Add(new ShotRecord {angle=angle,before=before,after=game.BlocksRemaining});
                if (game.BlocksRemaining == before) missed.Add(Mathf.RoundToInt(angle*2)); else missed.Clear();
            }
            record.shots = shots.ToArray(); records.Add(record);
            File.WriteAllText("TestResults/BounceLearning/solutions.json", JsonUtility.ToJson(new Report {boards=records.ToArray()}, true));
            Assert.That(game.BlocksRemaining, Is.Zero, "No clear route found within the authored budget: stage " + stage);
            // Replay the exact action sequence from a fresh board, with no search or state edits.
            game.Begin(stage, _ => {}); yield return null;
            foreach (var shot in record.shots)
            {
                Assert.That(game.BlocksRemaining, Is.EqualTo(shot.before));
                yield return FireAndSettle(shot.angle);
                Assert.That(game.BlocksRemaining, Is.EqualTo(shot.after), "Replay diverged at stage " + stage);
            }
            Assert.That(game.BlocksRemaining, Is.Zero); record.replayPassed = true;
            File.WriteAllText("TestResults/BounceLearning/solutions.json", JsonUtility.ToJson(new Report {boards=records.ToArray()}, true));
            Debug.Log("Learning stage " + stage + " cleared and replayed: " + shots.Count + "/" + record.budget + " balls");
        }
    }

    [UnityTest]
    public IEnumerator SmallAndLargeGridsFitPortraitAndKeepCollisionAligned()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);previousScale=Time.timeScale;
        if(KitApp.Instance!=null)Object.Destroy(KitApp.Instance.gameObject);yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        KitApp.Instance.Play(1);yield return Ready("VisualIngame");
        game=Object.FindFirstObjectByType<BounceModule>();
        var original=game.presentation.levels;var camera=Camera.main;camera.aspect=9f/16;
        float smallSize=0;
        try
        {
            foreach(var size in new[]{new Vector2Int(3,8),new Vector2Int(25,30)})
            {
                var rows=Enumerable.Repeat(new string('.',size.x),size.y).ToArray();
                rows[0]="1"+new string('.',size.x-1);rows[^1]=new string('.',size.x-1)+"9";
                var data=new BounceModule.Level {gridRows=size.y,balls=10,rows=rows,gimmicks=new[]{new GimmickSpec {type="mine",col=2,row=2}}};
                var asset=new TextAsset(JsonUtility.ToJson(new BounceModule.Levels {levels=new[]{data}}));
                game.presentation.levels=asset;game.Begin(1,_=>{});yield return null;
                Assert.That(GameObject.Find("Wall top").transform.position.z,Is.EqualTo(size.y-5));
                Assert.That(GameObject.Find("Wall left").transform.localScale.z,Is.EqualTo(size.y+1.5f));
                Physics.SyncTransforms();
                Assert.That(Physics.SphereCast(Origin+Vector3.forward*.9f,.25f,Vector3.forward,out var forwardHit,40,1<<10),Is.True);
                Assert.That(forwardHit.collider.name,Does.Not.StartWith("Bumper"),"Narrow grids must leave the launcher exit open");
                var mine=Object.FindFirstObjectByType<BounceGimmicks>().Targets.Single();
                Assert.That(mine.origin,Is.EqualTo(BounceGimmicks.Cell(2,2,size.x,size.y)));
                foreach(var point in new[]{new Vector3(-size.x*.5f-1,1,size.y-4),new Vector3(size.x*.5f+1,1,size.y-4),new Vector3(0,0,-10)})
                {
                    var viewport=camera.WorldToViewportPoint(point);
                    Assert.That(viewport.x,Is.InRange(0f,1f));Assert.That(viewport.y,Is.InRange(.02f,.98f));
                }
                if(smallSize==0)
                {
                    smallSize=camera.orthographicSize;
                    camera.aspect=.3f;yield return null;
                    Assert.That(camera.orthographicSize,Is.GreaterThan(smallSize),"Camera refits when the viewport changes");
                    camera.aspect=9f/16;yield return null;
                }
                else Assert.That(camera.orthographicSize,Is.GreaterThan(smallSize*2),"Large grid uses smaller on-screen cells");
                Directory.CreateDirectory("TestResults/BounceLearning");Capture(1,"grid-"+size.x+"x"+size.y+".png");
                game.End();game.presentation.levels=original;Object.Destroy(asset);yield return null;
            }
        }
        finally {game.presentation.levels=original;camera.ResetAspect();}
    }

    private IEnumerator FireAndSettle(float angle)
    {
        Time.timeScale = 12;
        Assert.That(game.Fire(Quaternion.Euler(0,angle,0)*Vector3.forward), Is.True);
        float end = Time.realtimeSinceStartup + 15;
        while (game.ActiveBalls > 0 && game.BlocksRemaining > 0 && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(game.ActiveBalls == 0 || game.BlocksRemaining == 0, Is.True, "Shot must drain or clear the board");
        Time.timeScale = 1; yield return null;
    }

    // Diagnostic route finder only: broad angle sweep, actual colliders, up to 8 wall reflections.
    // This is neither the proposed 1/2-move GRP1 cohort policy nor a human-win-rate estimator.
    private float FindVisibleShot(HashSet<int> missed)
    {
        Physics.SyncTransforms();
        float best = float.NegativeInfinity, chosen = 0;
        var board = Object.FindFirstObjectByType<BounceGimmicks>();
        for (int step = -150; step <= 150; step++)
        {
            if (missed.Contains(step)) continue;
            float angle = step*.5f;
            var direction = Quaternion.Euler(0,angle,0)*Vector3.forward;
            var position = Origin+direction*.9f;
            float distance = 0;
            for (int reflection = 0; reflection < 8; reflection++)
            {
                float limit = direction.z < 0 ? Mathf.Max(0,(BounceModule.DeadZoneZ-position.z)/direction.z) : 40;
                if (!Physics.SphereCast(position,game.presentation.ingameConfig.ballRadius,direction,out var hit,limit,1<<10,QueryTriggerInteraction.Ignore)) break;
                distance += hit.distance;
                var block = hit.collider.transform;
                while (block != null && !block.name.StartsWith("Block_")) block = block.parent;
                var mine = board.Targets.FirstOrDefault(t => !t.completed && t.spec.type == "mine" && hit.collider.transform.IsChildOf(t.root));
                if (block != null || mine != null)
                {
                    var center = mine != null ? mine.origin : block.position;
                    float lateral = Vector3.Cross(center-position,direction).magnitude;
                    float score = (mine != null ? 1000 : 100)-lateral*10-distance*.01f;
                    if (score > best) {best=score;chosen=angle;}
                    break;
                }
                position += direction*hit.distance;
                direction = (Vector3)Reflect.Invoke(null,new object[]{direction,hit.normal});
                position += direction*.001f;
            }
        }
        Assert.That(float.IsNegativeInfinity(best), Is.False, "Every remaining target must have a reachable shot");
        return chosen;
    }

    private static void Capture(int stage,string filename=null)
    {
        var camera = Camera.main;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        var target = new RenderTexture(900,1600,24) {antiAliasing=4}; target.Create();
        var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        Texture2D image = null;
        try
        {
            foreach (var canvas in canvases) {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;}
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
            RenderTexture.active=target;image=new Texture2D(900,1600,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,900,1600),0,0);image.Apply();
            File.WriteAllBytes("TestResults/BounceLearning/"+(filename??("level-"+stage.ToString("00")+".png")),image.EncodeToPNG());
        }
        finally
        {
            foreach (var canvas in canvases) canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            RenderTexture.active=previous;camera.targetTexture=oldTarget;
            if (image!=null) Object.Destroy(image); target.Release();Object.Destroy(target);
        }
    }

    private static IEnumerator Ready(string scene)
    {
        float end=Time.realtimeSinceStartup+30;
        while(Time.realtimeSinceStartup<end)
        {
            if(SceneManager.GetActiveScene().name==scene && KitApp.Instance!=null && !KitApp.Instance.IsLoading) {yield return null;yield break;}
            yield return null;
        }
        Assert.Fail("Scene not ready: "+scene);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale=previousScale == 0 ? 1 : previousScale;
        if(game!=null)game.End();
        if(KitApp.Instance!=null)Object.Destroy(KitApp.Instance.gameObject);yield return null;
        var scope=Object.FindFirstObjectByType<IntegratedProjectScope>();if(scope!=null)Object.Destroy(scope.gameObject);yield return null;
        foreach(var root in Object.FindObjectsByType<UIRoot>(FindObjectsSortMode.None))Object.Destroy(root.gameObject);yield return null;
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",null);
        if(folder!=null && Directory.Exists(folder))Directory.Delete(folder,true);
    }
}
