using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BK.Ads
{
    /// <summary>MAX 8.5.1 bridge adapted from GRP1's AdUnit pattern, without RootBox/backend/analytics dependencies.</summary>
    public sealed class AppLovinAdProvider : IAdProvider
    {
        private readonly AdSettings settings;
        private readonly AdUnits units;
        private readonly CancellationTokenSource lifetime=new();
        private readonly int[] attempts=new int[2];
        private readonly bool[] loading=new bool[2];
        private readonly int[] loadGeneration=new int[2];
        private bool subscribed, initialized, disposed, bannerCreated, bannerWanted;
        public event Action<AdFormat,AdEvent> Event;
        public AppLovinAdProvider(AdSettings settings) { this.settings=settings; units=settings.ActiveUnits; }
        public void Initialize()
        {
            if(subscribed || disposed || units==null)return;
            subscribed=true;
            MaxSdkBase.InvokeEventsOnUnityMainThread=true;
            MaxSdkCallbacks.OnSdkInitializedEvent+=Initialized;
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent+=RewardedLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent+=RewardedLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent+=RewardedDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent+=RewardedDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent+=RewardedEarned;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent+=RewardedClosed;
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent+=InterstitialLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent+=InterstitialLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent+=InterstitialDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent+=InterstitialDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent+=InterstitialClosed;
            MaxSdk.SetVerboseLogging(settings.verboseLogging);
            if(settings.testDeviceAdvertisingIds!=null && settings.testDeviceAdvertisingIds.Length>0)MaxSdk.SetTestDeviceAdvertisingIdentifiers(settings.testDeviceAdvertisingIds);
            if(MaxSdk.IsInitialized())StartLoading();else MaxSdk.InitializeSdk();
        }
        private void Initialized(MaxSdkBase.SdkConfiguration config)
        { if(!disposed && config!=null && config.IsSuccessfullyInitialized)StartLoading(); }
        private void StartLoading()
        { initialized=true; Load(AdFormat.Rewarded); Load(AdFormat.Interstitial); SetBannerVisible(bannerWanted); }
        private string Id(AdFormat format) => units==null?null:format==AdFormat.Rewarded?units.rewarded:units.interstitial;
        public bool IsReady(AdFormat format) => initialized && !disposed && !string.IsNullOrWhiteSpace(Id(format))
            && (format==AdFormat.Rewarded?MaxSdk.IsRewardedAdReady(Id(format)):MaxSdk.IsInterstitialReady(Id(format)));
        public void Load(AdFormat format)
        {
            if(!initialized || disposed || string.IsNullOrWhiteSpace(Id(format)) || loading[(int)format])return;
            loading[(int)format]=true;
            int generation=++loadGeneration[(int)format];
            if(format==AdFormat.Rewarded)MaxSdk.LoadRewardedAd(Id(format));else MaxSdk.LoadInterstitial(Id(format));
            LoadWatchdog(format,generation,lifetime.Token).Forget();
        }
        private async UniTaskVoid LoadWatchdog(AdFormat format,int generation,CancellationToken token)
        {
            if(await UniTask.Delay(TimeSpan.FromSeconds(10),DelayType.Realtime,cancellationToken:token).SuppressCancellationThrow())return;
            // A missing load callback must not permanently suppress later explicit requests.
            if(generation==loadGeneration[(int)format])loading[(int)format]=false;
        }
        private void Loaded(AdFormat format,string id)
        { if(id!=Id(format))return; loading[(int)format]=false;attempts[(int)format]=0; }
        private void LoadFailed(AdFormat format,string id)
        { if(id!=Id(format))return;loading[(int)format]=false;Retry(format,lifetime.Token).Forget(); }
        private async UniTaskVoid Retry(AdFormat format,CancellationToken token)
        {
            double seconds=Math.Pow(2,Math.Min(6,++attempts[(int)format]));
            if(!await UniTask.Delay(TimeSpan.FromSeconds(seconds),DelayType.Realtime,cancellationToken:token).SuppressCancellationThrow())Load(format);
        }
        public void Show(AdFormat format,string placement)
        { if(format==AdFormat.Rewarded)MaxSdk.ShowRewardedAd(Id(format),placement);else MaxSdk.ShowInterstitial(Id(format),placement); }
        private void Signal(AdFormat format,string id,AdEvent signal)
        { if(!disposed && id==Id(format))Event?.Invoke(format,signal); }
        private void Terminal(AdFormat format,string id,AdEvent signal)
        { Signal(format,id,signal); if(id==Id(format))Load(format); }
        private void RewardedLoaded(string id,MaxSdkBase.AdInfo info)=>Loaded(AdFormat.Rewarded,id);
        private void InterstitialLoaded(string id,MaxSdkBase.AdInfo info)=>Loaded(AdFormat.Interstitial,id);
        private void RewardedLoadFailed(string id,MaxSdkBase.ErrorInfo error)=>LoadFailed(AdFormat.Rewarded,id);
        private void InterstitialLoadFailed(string id,MaxSdkBase.ErrorInfo error)=>LoadFailed(AdFormat.Interstitial,id);
        private void RewardedDisplayed(string id,MaxSdkBase.AdInfo info)=>Signal(AdFormat.Rewarded,id,AdEvent.Displayed);
        private void InterstitialDisplayed(string id,MaxSdkBase.AdInfo info)=>Signal(AdFormat.Interstitial,id,AdEvent.Displayed);
        private void RewardedEarned(string id,MaxSdkBase.Reward reward,MaxSdkBase.AdInfo info)=>Signal(AdFormat.Rewarded,id,AdEvent.RewardEarned);
        private void RewardedClosed(string id,MaxSdkBase.AdInfo info)=>Terminal(AdFormat.Rewarded,id,AdEvent.Closed);
        private void InterstitialClosed(string id,MaxSdkBase.AdInfo info)=>Terminal(AdFormat.Interstitial,id,AdEvent.Closed);
        private void RewardedDisplayFailed(string id,MaxSdkBase.ErrorInfo error,MaxSdkBase.AdInfo info)=>Terminal(AdFormat.Rewarded,id,AdEvent.Failed);
        private void InterstitialDisplayFailed(string id,MaxSdkBase.ErrorInfo error,MaxSdkBase.AdInfo info)=>Terminal(AdFormat.Interstitial,id,AdEvent.Failed);
        public void SetBannerVisible(bool visible)
        {
            bannerWanted=visible;
            if(!initialized || disposed || string.IsNullOrWhiteSpace(units.banner))return;
            if(visible)
            {
                if(!bannerCreated)
                { MaxSdk.CreateBanner(units.banner,new MaxSdkBase.AdViewConfiguration(MaxSdkBase.AdViewPosition.BottomCenter));bannerCreated=true; }
                MaxSdk.ShowBanner(units.banner);MaxSdk.StartBannerAutoRefresh(units.banner);
            }
            else if(bannerCreated){MaxSdk.HideBanner(units.banner);MaxSdk.StopBannerAutoRefresh(units.banner);}
        }
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;lifetime.Cancel();lifetime.Dispose();
            if(!subscribed)return;
            MaxSdkCallbacks.OnSdkInitializedEvent-=Initialized;
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent-=RewardedLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent-=RewardedLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent-=RewardedDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent-=RewardedDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent-=RewardedEarned;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent-=RewardedClosed;
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent-=InterstitialLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent-=InterstitialLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent-=InterstitialDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent-=InterstitialDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent-=InterstitialClosed;
            if(bannerCreated)MaxSdk.DestroyBanner(units.banner);
            Event=null;
        }
    }
}
