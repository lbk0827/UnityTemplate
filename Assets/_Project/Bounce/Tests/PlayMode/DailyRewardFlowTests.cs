using System;
using System.Collections;
using System.IO;
using System.Linq;
using BK.Kit;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class DailyRewardFlowTests
{
    private string folder;

    [UnityTest]
    public IEnumerator LobbyButtonOpensThePopupAndClaimsGrantThroughTheWallet()
    {
        folder=Path.Combine(Path.GetTempPath(),"BKKitTests",Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("BK_KIT_TEST_SAVE_DIR",folder);
        if(KitApp.Instance!=null)UnityEngine.Object.Destroy(KitApp.Instance.gameObject);
        yield return null;
        yield return SceneManager.LoadSceneAsync("VisualBootstrap");
        yield return Ready("VisualLobby");
        var app=KitApp.Instance;
        var button=GameObject.Find("Daily button");
        Assert.That(button,Is.Not.Null,"Lobby shows a daily reward entry");
        var count=button.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Daily count");
        Assert.That(count.text,Is.EqualTo("5"),"day 1 + three bonus slots + hourly gift");
        Assert.That(app.Daily.ClaimableCount,Is.EqualTo(5));
        button.GetComponent<Button>().onClick.Invoke();
        yield return WaitOpen<DailyRewardPopup>();
        var popup=UnityEngine.Object.FindFirstObjectByType<DailyRewardPopup>();
        Assert.That(popup,Is.Not.Null);Assert.That(popup.IsOpen,Is.True);
        Assert.That(popup.DayButton(0).gameObject.activeInHierarchy,Is.True,"Day 1 is claimable");
        Assert.That(popup.DayButton(1).gameObject.activeInHierarchy,Is.False,"Day 2 waits for tomorrow");
        long gold=app.Gold;
        popup.DayButton(0).onClick.Invoke();yield return null;
        Assert.That(app.Gold,Is.EqualTo(gold+200),"Day 1 grants 200 gold");
        Assert.That(KitTestSaves.Gold(folder),Is.EqualTo(gold+200));
        Assert.That(KitTestSaves.DailyNextDay(folder),Is.EqualTo(1),"Progress is saved through DailyRewardsData");
        Assert.That(popup.DayButton(0).gameObject.activeInHierarchy,Is.False,"Claimed day shows Done");
        Assert.That(popup.DayButton(1).gameObject.activeInHierarchy,Is.False,"No catch-up on the same day");
        Assert.That(app.TryClaimDaily(out _),Is.False);
        Assert.That(popup.BonusButton(0).interactable,Is.True);
        Assert.That(popup.BonusButton(1).interactable,Is.False,"Bonus slots are claimed in order");
        popup.BonusButton(0).onClick.Invoke();yield return null;
        Assert.That(app.Gold,Is.EqualTo(gold+250));
        Assert.That(popup.BonusButton(0).interactable,Is.False);
        Assert.That(popup.BonusButton(1).interactable,Is.True);
        Assert.That(popup.HourlyButton.interactable,Is.True);
        popup.HourlyButton.onClick.Invoke();yield return null;
        Assert.That(app.Gold,Is.EqualTo(gold+270),"Hourly gift grants 20 gold");
        Assert.That(popup.HourlyButton.interactable,Is.False,"Hourly gift waits an hour");
        Assert.That(popup.HourlyButton.GetComponentInChildren<TMP_Text>().text,Does.StartWith("Next in"));
        Assert.That(KitTestSaves.Gold(folder),Is.EqualTo(gold+270));
        Assert.That(count.text,Is.EqualTo("2"),"Two bonus slots remain");
        yield return null;
        popup.CloseButton.onClick.Invoke();
        yield return new WaitForSecondsRealtime(.4f);
        Assert.That(UnityEngine.Object.FindFirstObjectByType<DailyRewardPopup>(),Is.Null,"Close releases the view");
        // Reopening shows the persisted state and the Play button still works underneath.
        button.GetComponent<Button>().onClick.Invoke();
        yield return WaitOpen<DailyRewardPopup>();
        popup=UnityEngine.Object.FindFirstObjectByType<DailyRewardPopup>();
        Assert.That(popup.DayButton(0).gameObject.activeInHierarchy,Is.False);
        Assert.That(popup.BonusButton(1).interactable,Is.True);
        popup.RequestClose();
        yield return new WaitForSecondsRealtime(.4f);
        var stage=UnityEngine.Object.FindObjectsByType<VisualBindings>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(b=>b.role=="StageInfo");
        Assert.That(stage.Get<Button>("buttons.0.button").interactable,Is.True);
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
