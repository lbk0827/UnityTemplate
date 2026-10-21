using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BK.Kit;
using BK.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// A sensitivity probe, not a calibrated novice/intermediate/expert or human win-rate model.
// Uses the live game, one ball at a time, no future-state rollout and no direct damage calls.
public sealed class BounceLearningDifficultyTests
{
    [Serializable] public sealed class Shot
    {
        public float intendedAngle, actualAngle, gameSeconds;
        public int before, after;
        public string target;
    }
    [Serializable] public sealed class Trial
    {
        public int stage, seed, budget, remaining, ballsLeft;
        public float errorDegrees, gameSeconds;
        public bool cleared, noDirectTarget;
        public Shot[] shots;
    }
    [Serializable] public sealed class Report
    {
        public string kind = "live-runtime-direct-aim-sensitivity-not-human-win-rate";
        public string policy = "Visible target centre; mines first, then nearest target; no planned wall bounce; one ball until drain; uniform independent per-shot angular error";
        public string timing = "Zero-error runs at normal speed; noisy runs at timeScale 12 with unchanged fixedDeltaTime; thinking time excluded";
        public int noisyTrialsPerStage = 12, seed = 20261021;
        public Trial[] trials;
    }

    private string folder, previousSaveFolder;
    private float previousScale;
    private BounceModule game;
    private static readonly Vector3 Origin = new Vector3(0, .5f, -8.5f);
    private const string Output = "TestResults/BounceDifficulty/trials.json";

    [UnityTest, Timeout(1200000)]
    public IEnumerator MeasureDirectAimWithZeroTwoAndFiveDegreeErrors()
    {
        previousSaveFolder = Environment.GetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR");
        folder = Path.Combine(Path.GetTempPath(), "BKKitTests", Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR", folder);
        previousScale = Time.timeScale;
        if (KitApp.Instance != null) Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap"); yield return Ready("VisualLobby");
        var app = KitApp.Instance; app.MarkGuideSeen("gimmick.mine");
        var report = new Report(); var records = new List<Trial>();
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        for (int stage = 1; stage <= 10; stage++)
        {
            Time.timeScale = 1;
            Assert.That(app.TrySetSandboxProgress(stage, 1000, 0), Is.True);
            app.Play(stage); yield return Ready("VisualIngame");
            game = Object.FindFirstObjectByType<BounceModule>();
            Assert.That(game.presentation.levels.name, Is.EqualTo("bounce-learning-levels"));
            foreach (float error in new[] {0f, 2f, 5f})
            {
                int count = error == 0 ? 1 : report.noisyTrialsPerStage;
                for (int attempt = 0; attempt < count; attempt++)
                {
                    int resolution = 0;
                    game.Begin(stage, won => resolution = won ? 1 : -1); yield return null;
                    var record = new Trial {stage=stage, seed=report.seed+stage*1000+attempt, errorDegrees=error, budget=game.BallsRemaining};
                    var random = new System.Random(record.seed);
                    var shots = new List<Shot>();
                    while (game.BlocksRemaining > 0 && game.BallsRemaining > 0)
                    {
                        if (!DirectTarget(out float angle, out string target)) {record.noDirectTarget=true;break;}
                        float noisyAngle = Mathf.Clamp(angle+(float)(random.NextDouble()*2-1)*error, -75, 75);
                        var shot = new Shot {intendedAngle=angle, actualAngle=noisyAngle, target=target, before=game.BlocksRemaining};
                        Time.timeScale = error == 0 ? 1 : 12;
                        float started = Time.time;
                        Assert.That(game.Fire(Quaternion.Euler(0, noisyAngle, 0)*Vector3.forward), Is.True);
                        float deadline = Time.realtimeSinceStartup + 30;
                        while (game.ActiveBalls > 0 && game.BlocksRemaining > 0 && Time.realtimeSinceStartup < deadline) yield return null;
                        Assert.That(game.ActiveBalls == 0 || game.BlocksRemaining == 0, Is.True, "Real shot must drain or clear");
                        shot.gameSeconds = Time.time-started; shot.after=game.BlocksRemaining; shots.Add(shot);
                        record.gameSeconds += shot.gameSeconds;
                    }
                    // Confirm the actual runtime win/loss callback, not just the target count.
                    if (!record.noDirectTarget)
                    {
                        float deadline = Time.realtimeSinceStartup+3;
                        while (resolution == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                        Assert.That(resolution, Is.EqualTo(game.BlocksRemaining == 0 ? 1 : -1));
                    }
                    record.cleared=resolution==1;record.remaining=game.BlocksRemaining;
                    record.ballsLeft=game.BallsRemaining;record.shots=shots.ToArray();records.Add(record);
                    Time.timeScale=1;report.trials=records.ToArray();
                    File.WriteAllText(Output, JsonUtility.ToJson(report, true));
                    game.End();yield return null;
                }
                var group=records.Where(r=>r.stage==stage && r.errorDegrees==error).ToArray();
                Debug.Log($"Aim probe L{stage} +/-{error} degrees: {group.Count(r=>r.cleared)}/{group.Length} clears, mean shots {group.Average(r=>r.shots.Length):F2}");
            }
        }
        Assert.That(records, Has.Count.EqualTo(250));
        Assert.That(records.SelectMany(r=>r.shots).All(s=>s.after<=s.before), Is.True);
    }

    private bool DirectTarget(out float angle, out string label)
    {
        Physics.SyncTransforms();angle=0;label=null;
        var board=Object.FindFirstObjectByType<BounceGimmicks>();
        var candidates=new List<(Transform root, Vector3 center, bool mine)>();
        foreach (Transform child in board.transform)
            if (child.gameObject.activeInHierarchy && child.name.StartsWith("Block_")) candidates.Add((child,child.position,false));
        foreach (var mine in board.Targets.Where(t=>!t.completed && t.spec.type=="mine")) candidates.Add((mine.root,mine.origin,true));
        float best=float.NegativeInfinity;
        foreach (var candidate in candidates.OrderBy(c=>c.center.x).ThenBy(c=>c.center.z))
        {
            Vector3 offset=candidate.center-Origin;offset.y=0;
            float aim=Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg;
            if (Mathf.Abs(aim)>75) continue;
            var direction=offset.normalized;
            if (!Physics.SphereCast(Origin+direction*.9f,game.presentation.ingameConfig.ballRadius,direction,out var hit,offset.magnitude,1<<10,QueryTriggerInteraction.Ignore)) continue;
            if (!hit.collider.transform.IsChildOf(candidate.root)) continue;
            float score=(candidate.mine?1000:0)-offset.magnitude;
            if (score<=best)continue;
            best=score;angle=aim;label=candidate.mine?"mine":candidate.root.name;
        }
        return label!=null;
    }

    private static IEnumerator Ready(string scene)
    {
        float deadline=Time.realtimeSinceStartup+30;
        while (KitApp.Instance==null || KitApp.Instance.IsLoading || SceneManager.GetActiveScene().name!=scene)
        {Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
        yield return null;yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale=1;
        if(game!=null)game.End();
        if(KitApp.Instance!=null)Object.Destroy(KitApp.Instance.gameObject);yield return null;
        var scope=Object.FindFirstObjectByType<IntegratedProjectScope>();if(scope!=null)Object.Destroy(scope.gameObject);yield return null;
        foreach(var root in Object.FindObjectsByType<UIRoot>(FindObjectsSortMode.None))Object.Destroy(root.gameObject);yield return null;
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",previousSaveFolder);
        if(folder!=null && Directory.Exists(folder))Directory.Delete(folder,true);
        Time.timeScale=previousScale;
    }
}
