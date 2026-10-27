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
using Object=UnityEngine.Object;

public sealed class BounceReleaseFlowTests
{
    [Serializable] public sealed class Shot { public float angle;public int waitTicks,before,after; }
    [Serializable] public sealed class Board
    {
        public int stage,budget,objectives,remaining,workRemaining,shotsUsed;
        public bool cleared,replayPassed;
        public string failure,screenshot;
        public Shot[] shots;
    }
    [Serializable] public sealed class Report
    {
        public string kind="production-FixedUpdate-physics-clear-and-replay";
        public string timing="Actual production FixedUpdate stepped at unchanged fixedDeltaTime; component automatic ticking disabled; explicit wait ticks recorded. No direct damage, boosters or extra balls.";
        public float fixedDeltaTime;
        public Board[] boards;
    }
    private BounceModule game;
    private string folder,previousFolder;
    private static readonly Vector3 Origin=new Vector3(0,.5f,-8.5f);
    private static readonly Action<BounceModule> Step=(Action<BounceModule>)Delegate.CreateDelegate(typeof(Action<BounceModule>),typeof(BounceModule).GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic));
    private static readonly Func<Vector3,Vector3,Vector3> Reflect=(Func<Vector3,Vector3,Vector3>)Delegate.CreateDelegate(typeof(Func<Vector3,Vector3,Vector3>),typeof(BounceModule).GetMethod("Reflect",BindingFlags.Static|BindingFlags.NonPublic));
    private const string Output="TestResults/BounceRelease/solutions.json";
    private static readonly int[] Screens={1,13,14,20,21,41,61,81,101,121,140};

    [UnityTest,Timeout(3600000)]
    public IEnumerator EveryReleasedStageClearsAndReplaysWithinBudget()
    {
        yield return Boot();
        var app=KitApp.Instance;
        foreach(var type in BounceReleasePolicy.Families)app.MarkGuideSeen("gimmick."+type);
        app.MarkGuideSeen("gimmick.door");
        app.Play(1);yield return Ready("VisualIngame");
        game=Object.FindFirstObjectByType<BounceModule>();
        Assert.That(game.presentation.levels.name,Is.EqualTo("bounce-release-levels"));
        var library=JsonUtility.FromJson<BounceModule.Levels>(game.presentation.levels.text);
        var records=new List<Board>();Directory.CreateDirectory(Path.GetDirectoryName(Output));
        for(int stage=1;stage<=library.levels.Length;stage++)
        {
            if(Screens.Contains(stage))
            {
                app.TrySetSandboxProgress(stage,1000,0);
                app.Play(stage);yield return Ready("VisualIngame");
                game=Object.FindFirstObjectByType<BounceModule>();
            }
            Camera.main.aspect=9f/16;
            game.enabled=false;game.Begin(stage,_=>{});yield return null;
            var record=new Board {stage=stage,budget=game.BallsRemaining,objectives=game.BlocksRemaining};
            if(Screens.Contains(stage))
            {
                record.screenshot="level-"+stage.ToString("000")+".png";
                Capture(record.screenshot);
            }
            var shots=new List<Shot>();var missed=new HashSet<int>();
            int waits=0;
            while(game.BlocksRemaining>0 && game.BallsRemaining>0)
            {
                float? angle=stage<=20 ? BounceOpeningPlanner.Choose(game,missed) : FindShot(game,missed);
                if(!angle.HasValue)
                {
                    if(waits>=100){record.failure="No reachable productive shot";break;}
                    for(int t=0;t<20;t++)Step(game);
                    waits+=20;missed.Clear();continue;
                }
                int before=game.BlocksRemaining,work=Work(game);
                Assert.That(game.Fire(Quaternion.Euler(0,angle.Value,0)*Vector3.forward),Is.True);
                int ticks=0;
                while(game.ActiveBalls>0 && game.BlocksRemaining>0 && ticks++<1100)Step(game);
                Assert.That(ticks,Is.LessThan(1101),"Shot must drain");
                shots.Add(new Shot {angle=angle.Value,waitTicks=waits,before=before,after=game.BlocksRemaining});waits=0;
                if(Work(game)>=work)missed.Add(Mathf.RoundToInt(angle.Value*4));else missed.Clear();
                yield return null;
            }
            record.shots=shots.ToArray();record.shotsUsed=shots.Count;record.remaining=game.BlocksRemaining;record.workRemaining=Work(game);
            record.cleared=game.BlocksRemaining==0;
            if(record.cleared)
            {
                game.Begin(stage,_=>{});yield return null;
                bool same=true;
                foreach(var shot in shots)
                {
                    for(int t=0;t<shot.waitTicks;t++)Step(game);
                    same &= game.BlocksRemaining==shot.before;
                    Assert.That(game.Fire(Quaternion.Euler(0,shot.angle,0)*Vector3.forward),Is.True);
                    int ticks=0;
                    while(game.ActiveBalls>0 && game.BlocksRemaining>0 && ticks++<1100)Step(game);
                    same &= game.BlocksRemaining==shot.after;
                    yield return null;
                }
                record.replayPassed=same && game.BlocksRemaining==0;
            }
            records.Add(record);
            File.WriteAllText(Output,JsonUtility.ToJson(new Report {fixedDeltaTime=Time.fixedDeltaTime,boards=records.ToArray()},true));
            Debug.Log($"Release L{stage}: {shots.Count}/{record.budget}, remaining {record.remaining}, replay {record.replayPassed}");
            game.End();yield return null;
        }
        Assert.That(records.Where(r=>!r.cleared || !r.replayPassed).Select(r=>$"L{r.stage}: {r.failure} ({r.remaining} left)"),Is.Empty);
    }

    private static int Work(BounceModule module)
    {
        var board=Object.FindFirstObjectByType<BounceGimmicks>();
        return module.BlocksRemaining+board.Targets.Sum(t=>Mathf.Max(0,t.Remaining-1))+
            board.Targets.Count(t=>t.spec.type=="switch" && !t.activated);
    }

    internal static float? FindShot(BounceModule module,HashSet<int> missed,bool nearest=false)
    {
        Physics.SyncTransforms();
        var board=Object.FindFirstObjectByType<BounceGimmicks>();
        float best=float.NegativeInfinity;float? choice=null;
        for(int step=-300;step<=300;step++)
        {
            if(missed.Contains(step))continue;
            float angle=step*.25f;var direction=Quaternion.Euler(0,angle,0)*Vector3.forward;
            var position=Origin+direction*.9f;float distance=0;
            var combos=new HashSet<BounceGimmicks.Target>();
            for(int bounce=0;bounce<40;bounce++)
            {
                float limit=direction.z<0?Mathf.Max(0,(BounceModule.DeadZoneZ-position.z)/direction.z):40;
                if(!Physics.SphereCast(position,module.presentation.ingameConfig.ballRadius,direction,out var hit,limit,1<<10,QueryTriggerInteraction.Ignore))break;
                distance+=hit.distance;if(distance>340)break;
                var block=hit.collider.transform;
                while(block!=null && !block.name.StartsWith("Block_"))block=block.parent;
                var target=board.Targets.FirstOrDefault(t=>!t.completed && hit.collider.transform.IsChildOf(t.root));
                float priority=-1;Vector3 center=hit.point;
                if(block!=null)
                {
                    priority=board.Targets.Any(t=>t.spec.type=="cupboard" && t.Remaining>0 && t.cells.Any(c=>BounceGimmicks.IsAdjacent(c,block.position)))?350:100;
                    center=block.position;
                }
                else if(target!=null)
                {
                    center=target.origin;
                    switch(target.spec.type)
                    {
                        case "mine":priority=1000;break;
                        case "switch":if(!target.activated)priority=900;break;
                        case "tube":priority=600;break;
                        case "armor":
                            if(Vector3.Dot(hit.normal,BounceGimmicks.WeakNormal(target.spec.weakSide))>.7f && Vector3.Dot(direction,BounceGimmicks.WeakNormal(target.spec.weakSide))<0)priority=500;
                            break;
                        case "combo":if(!combos.Add(target))priority=1200;break;
                    }
                }
                if(priority>=0)
                {
                    float score=nearest ? -distance-Vector3.Cross(center-position,direction).magnitude*.1f
                        : priority-Vector3.Cross(center-position,direction).magnitude*10-distance*.01f;
                    if(score>best){best=score;choice=angle;}
                    break;
                }
                position+=direction*hit.distance;direction=Reflect(direction,hit.normal);position+=direction*.001f;
            }
        }
        return choice;
    }

    [Serializable] public sealed class OpeningAudit
    {
        public int stage,plannedShots,nearestShots,nearestRemaining,recoveryShots,recoveryRemaining,tolerantShots,tolerantRemaining;
        public float firstAngle;
        public int[] openingDamage; // -1, -.5, 0, +.5, +1 degrees
        public int recoveryFirstDamage;
    }
    [Serializable] public sealed class OpeningReport
    {
        public string policy="Planned: static block/mine lookahead with +/-0.5 degree robustness. Baseline: nearest reachable first collision; no multi-hit lookahead. Both fire actual runtime balls with no extra balls.";
        public string sourceSha256;
        public OpeningAudit[] boards;
    }

    [UnityTest,Timeout(3600000)]
    public IEnumerator OpeningChoicesCompareNearestShotsAndRecoverFromAlternateOpening()
    {
        yield return Boot(); var app=KitApp.Instance; app.MarkGuideSeen("gimmick.mine");
        app.Play(1); yield return Ready("VisualIngame"); game=Object.FindFirstObjectByType<BounceModule>();
        var solutions=JsonUtility.FromJson<Report>(File.ReadAllText(Output)).boards;
        var audits=new List<OpeningAudit>();
        for(int stage=1;stage<=20;stage++)
        {
            var solution=solutions.Single(b=>b.stage==stage);
            Assert.That(solution.cleared && solution.replayPassed,Is.True);
            var audit=new OpeningAudit {stage=stage,plannedShots=solution.shotsUsed,firstAngle=solution.shots[0].angle,openingDamage=new int[5]};
            for(int offset=0;offset<5;offset++)
            {
                game.enabled=false;game.Begin(stage,_=>{});yield return null;
                int before=game.BlocksRemaining;
                yield return Shoot(Mathf.Clamp(audit.firstAngle+(offset-2)*.5f,-75,75));
                audit.openingDamage[offset]=before-game.BlocksRemaining;
            }
            game.Begin(stage,_=>{});yield return null;var missed=new HashSet<int>();
            while(game.BlocksRemaining>0 && game.BallsRemaining>0)
            {
                var angle=FindShot(game,missed,true);if(!angle.HasValue)break;
                int before=game.BlocksRemaining;yield return Shoot(angle.Value);audit.nearestShots++;
                if(game.BlocksRemaining==before)missed.Add(Mathf.RoundToInt(angle.Value*4));else missed.Clear();
            }
            audit.nearestRemaining=game.BlocksRemaining;
            game.Begin(stage,_=>{});yield return null;int initial=game.BlocksRemaining;
            yield return Shoot(-75);audit.recoveryFirstDamage=initial-game.BlocksRemaining;
            audit.recoveryShots=1;missed.Clear();
            while(game.BlocksRemaining>0 && game.BallsRemaining>0)
            {
                var angle=BounceOpeningPlanner.Choose(game,missed);if(!angle.HasValue)break;
                int before=game.BlocksRemaining;yield return Shoot(angle.Value);audit.recoveryShots++;
                if(game.BlocksRemaining==before)missed.Add(Mathf.RoundToInt(angle.Value*4));else missed.Clear();
            }
            audit.recoveryRemaining=game.BlocksRemaining;
            game.Begin(stage,_=>{});yield return null;missed.Clear();
            while(game.BlocksRemaining>0 && game.BallsRemaining>0)
            {
                var angle=BounceOpeningPlanner.Choose(game,missed);if(!angle.HasValue)break;
                int before=game.BlocksRemaining;
                yield return Shoot(Mathf.Clamp(angle.Value+(audit.tolerantShots%2==0?1:-1),-75,75));
                audit.tolerantShots++;
                if(game.BlocksRemaining==before)missed.Add(Mathf.RoundToInt(angle.Value*4));else missed.Clear();
            }
            audit.tolerantRemaining=game.BlocksRemaining;
            audits.Add(audit);
            string source;
            using(var hash=System.Security.Cryptography.SHA256.Create())
                source=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes("Assets/_Project/Bounce/Content/bounce-release-levels.json"))).Replace("-", "").ToLowerInvariant();
            File.WriteAllText("TestResults/BounceRelease/opening-audit.json",JsonUtility.ToJson(new OpeningReport {sourceSha256=source,boards=audits.ToArray()},true));
            Debug.Log($"Opening L{stage}: planned {audit.plannedShots}, nearest {audit.nearestShots} ({audit.nearestRemaining} left), recovery {audit.recoveryShots} ({audit.recoveryRemaining} left), +/-1 degree {audit.tolerantShots} ({audit.tolerantRemaining} left)");
            game.End();yield return null;
        }
        Assert.That(audits.Where(a=>a.recoveryRemaining>0).Select(a=>a.stage),Is.Empty,"Alternate first shot must remain recoverable");
        Assert.That(audits.Where(a=>a.tolerantRemaining>0).Select(a=>a.stage),Is.Empty,"Alternating +/-1 degree error must remain recoverable");
    }

    private IEnumerator Shoot(float angle)
    {
        Assert.That(game.Fire(Quaternion.Euler(0,angle,0)*Vector3.forward),Is.True);
        int ticks=0;
        while(game.ActiveBalls>0 && game.BlocksRemaining>0 && ticks++<1100)Step(game);
        Assert.That(ticks,Is.LessThan(1101));yield return null;
    }

    [UnityTest]
    public IEnumerator FirstMineGuideIsAtFourteenAndCampaignEndDoesNotChargeAHeart()
    {
        yield return Boot();var app=KitApp.Instance;
        Assert.That(app.ReleasedLevelCount,Is.EqualTo(140));
        app.TrySetSandboxProgress(14,1000,0);app.Play(14);yield return Ready("VisualIngame");yield return new WaitForSecondsRealtime(.4f);
        game=Object.FindFirstObjectByType<BounceModule>();
        var popup=Object.FindFirstObjectByType<CommonPopupView>();Assert.That(popup,Is.Not.Null);
        Assert.That(string.Join(" ",popup.GetComponentsInChildren<TMPro.TMP_Text>().Select(t=>t.text)),Does.Contain("Sea mine"));
        int balls=game.BallsRemaining;popup.Close();yield return new WaitForSecondsRealtime(.4f);
        Assert.That(game.BallsRemaining,Is.EqualTo(balls));Assert.That(app.HasSeenGuide("gimmick.mine"),Is.True);
        app.GoToLobby();yield return Ready("VisualLobby");
        app.TrySetSandboxProgress(141,1000,0);long hearts=app.Hearts;
        Assert.That(app.CampaignComplete,Is.True);Assert.That(app.CanEnter(141),Is.False);
        app.Play(141);yield return null;
        Assert.That(app.Hearts,Is.EqualTo(hearts));Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("VisualLobby"));
    }

    [UnityTest,Timeout(300000)]
    public IEnumerator RecordedMineIntroductionAlsoClearsWithAutomaticPhysics()
    {
        yield return Boot();var app=KitApp.Instance;app.MarkGuideSeen("gimmick.mine");
        app.TrySetSandboxProgress(14,1000,0);app.Play(14);yield return Ready("VisualIngame");
        game=Object.FindFirstObjectByType<BounceModule>();game.Begin(14,_=>{});yield return null;
        var record=JsonUtility.FromJson<Report>(File.ReadAllText(Output)).boards.Single(b=>b.stage==14);
        // Normal Unity scheduling at normal game speed, independently of the accelerated audit.
        foreach(var shot in record.shots)
        {
            Assert.That(game.BlocksRemaining,Is.EqualTo(shot.before));
            Assert.That(game.Fire(Quaternion.Euler(0,shot.angle,0)*Vector3.forward),Is.True);
            float end=Time.realtimeSinceStartup+25;
            while(game.ActiveBalls>0 && game.BlocksRemaining>0 && Time.realtimeSinceStartup<end)yield return null;
            Assert.That(game.BlocksRemaining,Is.EqualTo(shot.after));
        }
        Assert.That(game.BlocksRemaining,Is.Zero);
    }

    private IEnumerator Boot()
    {
        previousFolder=Environment.GetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR");
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);Time.timeScale=1;
        if(KitApp.Instance!=null)Object.Destroy(KitApp.Instance.gameObject);yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
    }
    private static IEnumerator Ready(string scene)
    {
        float end=Time.realtimeSinceStartup+30;
        while(KitApp.Instance==null || KitApp.Instance.IsLoading || SceneManager.GetActiveScene().name!=scene)
        {Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}
        yield return null;
    }
    private static void Capture(string name)
    {
        var camera=Camera.main;var target=new RenderTexture(600,1066,24){antiAliasing=4};target.Create();
        var previous=RenderTexture.active;var old=camera.targetTexture;
        var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
        try
        {
            foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
            camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest {destination=target});
            RenderTexture.active=target;var image=new Texture2D(600,1066,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,600,1066),0,0);image.Apply();File.WriteAllBytes("TestResults/BounceRelease/"+name,image.EncodeToPNG());Object.Destroy(image);
        }
        finally {foreach(var c in canvases)c.renderMode=RenderMode.ScreenSpaceOverlay;camera.targetTexture=old;RenderTexture.active=previous;target.Release();Object.Destroy(target);}
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale=1;if(game!=null)game.End();
        if(KitApp.Instance!=null)Object.Destroy(KitApp.Instance.gameObject);yield return null;
        var scope=Object.FindFirstObjectByType<IntegratedProjectScope>();if(scope!=null)Object.Destroy(scope.gameObject);yield return null;
        foreach(var root in Object.FindObjectsByType<UIRoot>(FindObjectsSortMode.None))Object.Destroy(root.gameObject);yield return null;
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",previousFolder);
        if(folder!=null && Directory.Exists(folder))Directory.Delete(folder,true);
    }
}
