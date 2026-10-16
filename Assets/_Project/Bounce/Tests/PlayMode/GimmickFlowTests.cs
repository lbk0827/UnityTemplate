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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public sealed class GimmickFlowTests
{
    private string folder;
    private BounceModule game;
    private static readonly MethodInfo HitBlock = typeof(BounceModule).GetMethod("HitBlock", BindingFlags.Instance | BindingFlags.NonPublic);

    [UnitySetUp]
    public IEnumerator Setup()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");yield return Ready("VisualLobby");
        KitApp.Instance.Play(1);yield return Ready("VisualIngame");
        game=Object.FindFirstObjectByType<BounceModule>();
    }

    private BounceGimmicks Begin(int stage)
    {
        game.Begin(stage,_=>{});
        return Object.FindFirstObjectByType<BounceGimmicks>();
    }

    [UnityTest]
    public IEnumerator MineDestroysAllEightNeighboursAndCannotExplodeTwice()
    {
        var board=Begin(17);var mine=board.Targets.Single();
        Assert.That(game.BlocksRemaining,Is.EqualTo(9));
        Capture("gimmick-mine-before");
        Assert.That(board.Hit(mine.collider,1),Is.True);
        Assert.That(game.BlocksRemaining,Is.Zero);
        Assert.That(board.Hit(mine.collider,1),Is.False);
        yield return null;Capture("gimmick-mine-explosion");
    }

    [UnityTest]
    public IEnumerator MineChainTerminatesAndHitsLongTubeOnlyOncePerBlast()
    {
        var board=Begin(18);var tube=board.Targets.Single(t=>t.spec.type=="tube");
        var mine=board.Targets.First(t=>t.spec.type=="mine");
        Assert.That(tube.Remaining,Is.EqualTo(2));
        board.Hit(mine.collider,1);
        Assert.That(board.Targets.Where(t=>t.spec.type=="mine").All(t=>t.completed),Is.True);
        Assert.That(tube.Remaining,Is.EqualTo(1));
        Assert.That(game.BlocksRemaining,Is.EqualTo(1));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SwitchOnlyOpensItsLinkedGateAndGateIsNotAnObjective()
    {
        var board=Begin(19);var button=board.Targets.Single(t=>t.spec.type=="switch");
        var gate=board.Targets.Single(t=>t.spec.type=="door");
        Assert.That(board.ObjectivesRemaining,Is.Zero);
        gate.spec.link="B";board.Hit(button.collider,1);
        Assert.That(gate.collider.enabled,Is.True,"Different group stays closed");
        board=Begin(19);button=board.Targets.Single(t=>t.spec.type=="switch");gate=board.Targets.Single(t=>t.spec.type=="door");
        Capture("gimmick-gate-closed");
        Assert.That(board.Hit(gate.collider,1),Is.False);
        board.Hit(button.collider,1);
        Assert.That(gate.collider.enabled,Is.False);
        Assert.That(button.view.activeVisual.activeSelf,Is.True);
        board.Hit(button.collider,2);
        Assert.That(gate.collider.enabled,Is.False);
        yield return new WaitForSeconds(.35f);Capture("gimmick-gate-open");
    }

    [UnityTest]
    public IEnumerator DoubleHitRequiresTheSameLivingBallAndResetsAfterDrain()
    {
        var board=Begin(20);var combo=board.Targets.Single();
        board.Hit(combo.collider,11);board.Hit(combo.collider,12);
        Assert.That(combo.Remaining,Is.EqualTo(1),"Two different balls cannot complete a combo");
        Assert.That(combo.view.activeVisual.activeSelf,Is.True);
        Capture("gimmick-combo-armed");
        board.ShotEnded(11);Assert.That(combo.view.activeVisual.activeSelf,Is.True);
        board.ShotEnded(12);Assert.That(combo.view.activeVisual.activeSelf,Is.False);
        board.Hit(combo.collider,13);board.Hit(combo.collider,13);
        Assert.That(game.BlocksRemaining,Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ArmorRejectsWrongFaceAndAcceptsPhysicsHitOnConfiguredWeakSide()
    {
        var board=Begin(21);var armor=board.Targets.Single();
        Assert.That(board.Hit(armor.collider,1,Vector3.forward,Vector3.back),Is.False);
        Assert.That(armor.Remaining,Is.EqualTo(1));Capture("gimmick-armor");
        Assert.That(Physics.SphereCast(armor.origin+Vector3.right*1.5f,.25f,Vector3.left,out var hit,2,1<<10),Is.True);
        Assert.That(hit.collider,Is.EqualTo(armor.collider));
        Assert.That(board.Hit(hit.collider,1,Vector3.left,hit.normal),Is.True);
        Assert.That(game.BlocksRemaining,Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator CombinedBoardUsesPrefabBindingsAndPowerHitsBreakSpecialArmor()
    {
        var board=Begin(22);
        foreach(var t in board.Targets)
        {
            Assert.That(t.view,Is.Not.Null);Assert.That(t.view.GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0));
            Assert.That(t.view.GetComponentsInChildren<TMPro.TMP_Text>(),Is.Empty);
        }
        yield return null;Capture("gimmick-all-prefabs");
        foreach(var t in board.Targets.Where(t=>t.spec.type=="combo" || t.spec.type=="armor"))
        {
            board.HitArea(p=>(p-t.origin).sqrMagnitude<.1f);
            Assert.That(t.completed,Is.True);
        }
    }

    [UnityTest]
    public IEnumerator TubeShortensFromEndAndReleasesCollisionImmediately()
    {
        var board=Begin(11);var tube=board.Targets.Single();
        Assert.That(game.BlocksRemaining,Is.EqualTo(1));
        Assert.That(tube.Remaining,Is.EqualTo(3));
        Assert.That(tube.furnace,Is.Not.Null);
        Assert.That(tube.collider.enabled,Is.True);
        var first=tube.pieces[0].GetComponent<Collider>();
        var last=tube.pieces[2].GetComponent<Collider>();var removedPosition=last.transform.position;
        Capture("gimmick-tube-before");
        Assert.That(board.Hit(first),Is.True);
        Assert.That(tube.Remaining,Is.EqualTo(2));
        Assert.That(last.enabled,Is.False);
        Assert.That(first.enabled,Is.True);
        Assert.That(Physics.SphereCast(removedPosition+Vector3.back*2,.25f,Vector3.forward,out _,3,1<<10),Is.False);
        yield return new WaitForSeconds(.35f);
        Capture("gimmick-tube-after");
        board.Hit(first);board.Hit(first);
        Assert.That(board.ObjectivesRemaining,Is.Zero);
        Assert.That(game.BlocksRemaining,Is.Zero);
        Assert.That(tube.collider.enabled,Is.False,"Completed furnace stops blocking shots");
        yield return null;
    }

    [UnityTest]
    public IEnumerator FurnaceHitsFeedThePipeAndCoolAfterCompletion()
    {
        var board=Begin(11);var tube=board.Targets.Single();
        Assert.That(board.Hit(tube.collider),Is.True);
        Assert.That(tube.Remaining,Is.EqualTo(2));
        yield return null;
        Capture("gimmick-furnace-feeding");
        board.Hit(tube.collider);board.Hit(tube.collider);
        yield return new WaitForSeconds(.4f);
        Assert.That(tube.collider.enabled,Is.False);
        var properties=new MaterialPropertyBlock();
        tube.furnaceGlow[0].GetPropertyBlock(properties);
        Assert.That(properties.GetColor("_EmissionColor").maxColorComponent,Is.EqualTo(0));
        Capture("gimmick-furnace-cooled");
    }

    [UnityTest]
    public IEnumerator CupboardConsumesExactlyTenAdjacentHitsAndLeavesNoBlockingCollider()
    {
        var board=Begin(14);var cupboard=board.Targets.Single();
        Assert.That(cupboard.Remaining,Is.EqualTo(10));
        Assert.That(board.Hit(cupboard.collider),Is.False,"Direct hits only reflect");
        board.NotifyBlockHit(cupboard.origin+new Vector3(-1,0,1));
        Assert.That(cupboard.Remaining,Is.EqualTo(10),"Diagonal hits do not count");
        Capture("gimmick-cupboard-before");
        for(int i=0;i<10;i++)
        {
            HitBlock.Invoke(game,new object[]{0,Vector3.forward});
            Assert.That(cupboard.Remaining,Is.EqualTo(9-i));
            if(i==4) {yield return new WaitForSeconds(.45f);Capture("gimmick-cupboard-half");}
        }
        Assert.That(game.BlocksRemaining,Is.Zero);
        Assert.That(cupboard.collider.enabled,Is.False);
        board.NotifyBlockHit(cupboard.origin+Vector3.left);
        Assert.That(cupboard.Remaining,Is.Zero);
        yield return new WaitForSeconds(.45f);
        Capture("gimmick-cupboard-empty");
    }

    [UnityTest]
    public IEnumerator PanelsReverseStayInBoundsAndAreNotObjectives()
    {
        var board=Begin(15);var panel=board.Targets.Single();
        Assert.That(board.ObjectivesRemaining,Is.Zero);
        Assert.That(game.BlocksRemaining,Is.EqualTo(10));
        board.Tick(Mathf.PI/(2*panel.spec.speed));
        Assert.That(panel.root.position.x,Is.EqualTo(panel.origin.x+panel.spec.travel).Within(.01f));
        Capture("gimmick-panel-right");
        board.Tick(Mathf.PI/panel.spec.speed);
        Assert.That(panel.root.position.x,Is.EqualTo(panel.origin.x-panel.spec.travel).Within(.01f));
        Capture("gimmick-panel-left");
        var position=panel.root.position+Vector3.back*.15f;var direction=Vector3.forward;
        board.SeparateFromPanels(ref position,ref direction,.25f);
        Assert.That(direction.z,Is.LessThan(0));
        Assert.That(position.z,Is.LessThan(panel.collider.bounds.min.z-.25f));
        board.Tick(Mathf.PI/(2*panel.spec.speed));
        var points=new Vector3[3];
        Assert.That(BounceModule.TraceTrajectory(Vector3.forward,points),Is.EqualTo(2),"Moving panel ends the prediction");
        for(int i=0;i<10;i++)HitBlock.Invoke(game,new object[]{0,Vector3.forward});
        Assert.That(game.BlocksRemaining,Is.Zero,"Indestructible panel cannot block completion");
        yield return null;
    }

    [UnityTest]
    public IEnumerator BoosterCanTargetATubeWithoutOrdinaryBlocks()
    {
        KitApp.Instance.TrySetSandboxProgress(11,1000,5);
        var board=Begin(11);var tube=board.Targets.Single();
        Assert.That(game.TryUseBooster(BoosterKind.Missile),Is.True);
        float until=Time.realtimeSinceStartup+10;
        while(game.IsUsingBooster && Time.realtimeSinceStartup<until)yield return null;
        Assert.That(game.IsUsingBooster,Is.False);
        Assert.That(tube.Remaining,Is.EqualTo(2),"A booster removes one tube cell, not the entire pipe");
        board.HitArea(_=>true);
        Assert.That(tube.Remaining,Is.EqualTo(1),"An area hit counts once per tube");
    }

    [UnityTest]
    public IEnumerator RealBallHitsTubeAndRestartRemovesOldGimmicks()
    {
        var board=Begin(11);var tube=board.Targets.Single();
        Assert.That(game.Fire(Vector3.forward),Is.True);
        float until=Time.realtimeSinceStartup+5;
        while(tube.Remaining==3 && Time.realtimeSinceStartup<until)yield return null;
        Assert.That(tube.Remaining,Is.EqualTo(2));
        Begin(1);yield return null;
        Assert.That(Object.FindObjectsByType<BounceGimmicks>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
        Assert.That(game.BlocksRemaining,Is.EqualTo(24));
    }

    [UnityTest]
    public IEnumerator AllDemoBoardsHaveNonOverlappingCellsAndEnoughCupboardNeighbours()
    {
        var levels=JsonUtility.FromJson<BounceModule.Levels>(game.presentation.levels.text).levels;
        for(int stage=11;stage<=16;stage++)
        {
            var board=Begin(stage);var data=levels[stage-1];
            var occupied=new System.Collections.Generic.HashSet<Vector3>();
            foreach(var t in board.Targets.Where(t=>t.spec.type!="panel"))
            {
                foreach(var cell in t.cells)
                {
                    Assert.That(occupied.Add(cell),Is.True,"Overlapping gimmick cells in stage "+stage);
                    int col=Mathf.RoundToInt(cell.x+4),row=Mathf.RoundToInt(7-cell.z);
                    Assert.That(col,Is.InRange(0,8));Assert.That(row,Is.InRange(0,12));
                    Assert.That(data.rows[row][col],Is.EqualTo('.'),"Gimmick overlaps ordinary block");
                }
                if(t.spec.type=="cupboard")
                {
                    int neighbours=0;
                    for(int row=0;row<data.rows.Length;row++)for(int col=0;col<9;col++)
                        if(char.IsDigit(data.rows[row][col]) && t.cells.Any(c=>BounceGimmicks.IsAdjacent(c,BounceGimmicks.Cell(col,row,9))))neighbours++;
                    Assert.That(neighbours,Is.GreaterThanOrEqualTo(10));
                }
            }
            Assert.That(board.Targets.SelectMany(t=>t.root.GetComponentsInChildren<TMPro.TMP_Text>()),Is.Empty,"No numeric durability labels");
            yield return null;
        }
        Capture("gimmick-all");
    }

    private static void Capture(string name)
    {
        Directory.CreateDirectory("TestResults");
        var camera=Camera.main;var target=new RenderTexture(900,1600,24);
        var previous=RenderTexture.active;var oldTarget=camera.targetTexture;
        camera.targetTexture=target;
        RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
        RenderTexture.active=target;
        var image=new Texture2D(900,1600,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,900,1600),0,0);image.Apply();
        File.WriteAllBytes("TestResults/"+name+".png",image.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=oldTarget;
        Object.Destroy(image);target.Release();Object.Destroy(target);
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
        if(game!=null)game.End();
        if(KitApp.Instance!=null)Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        var scope=Object.FindFirstObjectByType<IntegratedProjectScope>();
        if(scope!=null)Object.Destroy(scope.gameObject);
        yield return null;
        foreach(var root in Object.FindObjectsByType<UIRoot>(FindObjectsSortMode.None))Object.Destroy(root.gameObject);
        yield return null;
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",null);
        if(folder!=null && Directory.Exists(folder))Directory.Delete(folder,true);
        Time.timeScale=1;
    }
}
