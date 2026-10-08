using System;
using System.Collections;
using System.IO;
using System.Linq;
using BK.Kit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class WinStreakFlowTests
{
    private string folder;

    [UnityTest]
    public IEnumerator PlayOpensThePrePlayPopupAndStreakRewardsReachTheNextRound()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");
        yield return Ready("VisualLobby");
        var app=KitApp.Instance;
        Assert.That(app.Streak.Current.CurrentValue,Is.Zero);
        Stage().Get<Button>("buttons.0.button").onClick.Invoke();
        yield return WaitOpen<WinStreakPopup>();
        var popup=UnityEngine.Object.FindFirstObjectByType<WinStreakPopup>();
        Assert.That(popup,Is.Not.Null,"Play opens the sf pre-play popup instead of loading at once");
        Assert.That(popup.Level,Is.EqualTo(1));
        Assert.That(popup.GaugeFill,Is.Zero);
        Assert.That(SceneManager.GetActiveScene().name,Is.EqualTo("VisualLobby"));
        popup.CloseButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(UnityEngine.Object.FindFirstObjectByType<WinStreakPopup>(),Is.Null,"Close keeps the player in the lobby");
        Stage().Get<Button>("buttons.0.button").onClick.Invoke();
        yield return WaitOpen<WinStreakPopup>();
        popup=UnityEngine.Object.FindFirstObjectByType<WinStreakPopup>();
        popup.PlayButton.onClick.Invoke();
        yield return null;
        Assert.That(popup.IsOpen,Is.False,"Play starts closing the popup at once");
        yield return Ready("VisualIngame");
        Assert.That(UnityEngine.Object.FindFirstObjectByType<WinStreakPopup>(),Is.Null,"The popup closes when the round starts");
        app.Complete(true);
        Assert.That(app.Streak.Current.CurrentValue,Is.EqualTo(1));
        Assert.That(KitTestSaves.WinStreak(folder),Is.EqualTo(1),"Streak is saved with the clear");
        app.GoToLobby();yield return Ready("VisualLobby");
        Assert.That(app.Streak.Current.CurrentValue,Is.EqualTo(1),"Returning after a clear keeps the streak");
        app.Play(2);yield return Ready("VisualIngame");app.Complete(true);
        app.Play(3);yield return Ready("VisualIngame");app.Complete(true);
        Assert.That(app.Streak.Current.CurrentValue,Is.EqualTo(3));
        Assert.That(app.Streak.Pending.Count,Is.EqualTo(1),"Milestone 3 queues a booster for the next round");
        Assert.That(app.BoosterCount(BoosterKind.Missile),Is.Zero,"Not granted until the next round starts");
        app.GoToLobby();yield return Ready("VisualLobby");
        yield return SlotTransitionDone(); // the lobby replays the stage advance first and ignores Play meanwhile
        Stage().Get<Button>("buttons.0.button").onClick.Invoke();
        yield return WaitOpen<WinStreakPopup>();
        popup=UnityEngine.Object.FindFirstObjectByType<WinStreakPopup>();
        Assert.That(popup,Is.Not.Null,"Play opens the pre-play popup once the slot transition is over");
        Assert.That(popup.Level,Is.EqualTo(4));
        Assert.That(popup.GaugeFill,Is.EqualTo(1f/3f).Within(.01f),"Three milestones share the gauge equally");
        Assert.That(popup.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t=>t.text.Contains("Next round bonus") && t.text.Contains("Missile")),Is.True);
        popup.PlayButton.onClick.Invoke();
        yield return Ready("VisualIngame");
        Assert.That(app.BoosterCount(BoosterKind.Missile),Is.EqualTo(1),"The streak reward is credited when the round starts");
        Assert.That(app.Streak.Pending,Is.Empty);
        Assert.That(KitTestSaves.Booster(folder,BoosterKind.Missile),Is.EqualTo(1));
        app.Complete(false);
        app.GoToLobby();yield return Ready("VisualLobby");
        Assert.That(app.Streak.Current.CurrentValue,Is.Zero,"Leaving a lost round forfeits the streak");
        Assert.That(KitTestSaves.WinStreak(folder),Is.Zero);
    }

    private static VisualBindings Stage()
        =>UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).First(b=>b.role=="StageInfo");
    private static IEnumerator SlotTransitionDone()
    {
        float limit=Time.realtimeSinceStartup+5;
        StagePathView path;
        while((path=UnityEngine.Object.FindFirstObjectByType<StagePathView>())!=null && path.IsAdvancing && Time.realtimeSinceStartup<limit)yield return null;
        yield return null;
    }
    private static IEnumerator WaitOpen<T>() where T:BouncePopupView
    {
        float limit=Time.realtimeSinceStartup+5;
        T view;
        while(((view=UnityEngine.Object.FindFirstObjectByType<T>())==null || !view.IsOpen) && Time.realtimeSinceStartup<limit)yield return null;
        yield return null;
    }
    private static IEnumerator Ready(string name)
    {
        float limit=Time.realtimeSinceStartup+30;
        do {yield return null;Assert.That(Time.realtimeSinceStartup,Is.LessThan(limit));}
        while(KitApp.Instance==null || KitApp.Instance.IsLoading || SceneManager.GetActiveScene().name!=name);
        yield return null;yield return null;
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
