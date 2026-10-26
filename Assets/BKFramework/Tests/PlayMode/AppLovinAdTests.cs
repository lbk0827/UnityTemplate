using System;
using System.Collections;
using BK.Ads;
using BK.Core.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BK.Tests
{
    public sealed class AppLovinAdTests
    {
        private sealed class Clock : IClock { public DateTime UtcNow=>new(2026,1,1,0,0,0,DateTimeKind.Utc); }
        [UnityTest] public IEnumerator OfficialMaxStubCancelsRewardsAndClosesInterstitial()
        {
            var settings=ScriptableObject.CreateInstance<AdSettings>();settings.interstitialCooldownSeconds=0;
            using var ads=new AdService(new AppLovinAdProvider(settings),settings,new Clock(),()=>false);
            try
            {
                ads.Initialize();yield return Wait(()=>ads.IsReady(AdFormat.Rewarded));
                var pending=ads.ShowAsync(AdFormat.Rewarded,12,"test_cancel");
                yield return Wait(()=>GameObject.Find("MaxRewardedCloseButton")!=null);
                GameObject.Find("MaxRewardedCloseButton").GetComponent<Button>().onClick.Invoke();
                yield return Wait(()=>!ads.IsBusy);
                Assert.That(pending.GetAwaiter().GetResult(),Is.EqualTo(AdResult.ClosedWithoutReward));
                yield return null;
                yield return Wait(()=>ads.IsReady(AdFormat.Rewarded));
                pending=ads.ShowAsync(AdFormat.Rewarded,12,"test_reward");
                yield return Wait(()=>GameObject.Find("MaxRewardButton")!=null);
                GameObject.Find("MaxRewardButton").GetComponent<Button>().onClick.Invoke();
                GameObject.Find("MaxRewardedCloseButton").GetComponent<Button>().onClick.Invoke();
                yield return Wait(()=>!ads.IsBusy);
                Assert.That(pending.GetAwaiter().GetResult(),Is.EqualTo(AdResult.Rewarded));
                yield return null;
                yield return Wait(()=>ads.IsReady(AdFormat.Interstitial));
                pending=ads.ShowAsync(AdFormat.Interstitial,12,"test_clear");
                yield return Wait(()=>GameObject.Find("MaxInterstitialCloseButton")!=null);
                GameObject.Find("MaxInterstitialCloseButton").GetComponent<Button>().onClick.Invoke();
                yield return Wait(()=>!ads.IsBusy);
                Assert.That(pending.GetAwaiter().GetResult(),Is.EqualTo(AdResult.Completed));
            }
            finally{UnityEngine.Object.Destroy(settings);}
        }
        private static IEnumerator Wait(Func<bool> ready)
        {
            var deadline=Time.realtimeSinceStartup+20;
            while(!ready() && Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(ready(),Is.True,"MAX editor stub timed out.");
        }
    }
}
