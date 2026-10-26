using System;
using BK.Ads;
using NUnit.Framework;
using UnityEngine;

namespace BK.Tests
{
    public sealed class AdServiceTests
    {
        private sealed class Provider : IAdProvider
        {
            public event Action<AdFormat,AdEvent> Event;
            public bool Ready=true, Banner;
            public int Shows;
            public void Initialize() { }
            public bool IsReady(AdFormat format)=>Ready;
            public void Load(AdFormat format) { }
            public void Show(AdFormat format,string placement)=>Shows++;
            public void SetBannerVisible(bool visible)=>Banner=visible;
            public void Send(AdFormat format,AdEvent signal)=>Event?.Invoke(format,signal);
            public void Dispose() { }
        }
        private AdSettings settings;
        private Provider provider;
        private FakeClock clock;
        private AdService ads;
        private bool removed;
        [SetUp] public void SetUp()
        {
            settings=ScriptableObject.CreateInstance<AdSettings>();provider=new Provider();
            clock=new FakeClock(new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc));removed=false;
            ads=new AdService(provider,settings,clock,()=>removed);
        }
        [TearDown] public void TearDown(){ads.Dispose();UnityEngine.Object.DestroyImmediate(settings);}
        [Test] public void RewardRequiresEarnedCallbackAndCloseOnlyOnce()
        {
            var task=ads.ShowAsync(AdFormat.Rewarded,1,"heart");
            provider.Send(AdFormat.Rewarded,AdEvent.Closed);
            Assert.That(task.GetAwaiter().GetResult(),Is.EqualTo(AdResult.ClosedWithoutReward));
            task=ads.ShowAsync(AdFormat.Rewarded,1,"heart");
            provider.Send(AdFormat.Rewarded,AdEvent.RewardEarned);
            provider.Send(AdFormat.Rewarded,AdEvent.RewardEarned);
            Assert.That(ads.IsBusy,Is.True);
            provider.Send(AdFormat.Rewarded,AdEvent.Closed);
            Assert.That(task.GetAwaiter().GetResult(),Is.EqualTo(AdResult.Rewarded));
            provider.Send(AdFormat.Rewarded,AdEvent.Closed);
            Assert.That(ads.IsBusy,Is.False);
        }
        [Test] public void FullscreenRequestsCannotOverlapOrCrossClaimCallbacks()
        {
            var task=ads.ShowAsync(AdFormat.Rewarded,12,"heart");
            Assert.That(ads.ShowAsync(AdFormat.Interstitial,12,"clear").GetAwaiter().GetResult(),Is.EqualTo(AdResult.Busy));
            provider.Send(AdFormat.Interstitial,AdEvent.Closed);
            Assert.That(ads.IsBusy,Is.True);
            provider.Send(AdFormat.Rewarded,AdEvent.Failed);
            Assert.That(task.GetAwaiter().GetResult(),Is.EqualTo(AdResult.Failed));
            Assert.That(provider.Shows,Is.EqualTo(1));
        }
        [Test] public void NoAdsBlocksForcedAdsButKeepsOptionalRewarded()
        {
            ads.SetBannerVisible(true);Assert.That(provider.Banner,Is.True);
            removed=true;ads.RefreshBanner();Assert.That(provider.Banner,Is.False);
            Assert.That(ads.ShowAsync(AdFormat.Interstitial,99,"clear").GetAwaiter().GetResult(),Is.EqualTo(AdResult.Disabled));
            var reward=ads.ShowAsync(AdFormat.Rewarded,99,"heart");
            provider.Send(AdFormat.Rewarded,AdEvent.RewardEarned);provider.Send(AdFormat.Rewarded,AdEvent.Closed);
            Assert.That(reward.GetAwaiter().GetResult(),Is.EqualTo(AdResult.Rewarded));
        }
        [Test] public void StageAndCooldownUseDisplayedEventsIncludingRewarded()
        {
            Assert.That(ads.CanShowInterstitial(11),Is.False);Assert.That(ads.CanShowInterstitial(12),Is.True);
            var task=ads.ShowAsync(AdFormat.Rewarded,12,"heart");
            provider.Send(AdFormat.Rewarded,AdEvent.Displayed);provider.Send(AdFormat.Rewarded,AdEvent.Closed);
            task.GetAwaiter().GetResult();
            Assert.That(ads.CanShowInterstitial(12),Is.False);
            clock.Advance(TimeSpan.FromSeconds(90));Assert.That(ads.CanShowInterstitial(12),Is.True);
        }
        [Test] public void NotReadyAndDisposeDoNotStrandTheCaller()
        {
            provider.Ready=false;
            Assert.That(ads.ShowAsync(AdFormat.Rewarded,1,"heart").GetAwaiter().GetResult(),Is.EqualTo(AdResult.NotReady));
            provider.Ready=true;var task=ads.ShowAsync(AdFormat.Rewarded,1,"heart");
            ads.Dispose();Assert.That(task.GetAwaiter().GetResult(),Is.EqualTo(AdResult.Cancelled));
        }
    }
}
