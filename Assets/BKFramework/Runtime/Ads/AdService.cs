using System;
using System.Threading;
using BK.Core.Time;
using Cysharp.Threading.Tasks;

namespace BK.Ads
{
    /// <summary>One fullscreen request across all formats. Only a reward callback followed by close earns a reward.</summary>
    public sealed class AdService : IDisposable
    {
        private readonly IAdProvider provider;
        private readonly IClock clock;
        private readonly Func<bool> removed;
        private readonly AdSettings settings;
        private UniTaskCompletionSource<AdResult> completion;
        private CancellationTokenSource timeout;
        private AdFormat activeFormat;
        private bool earned, disposed, bannerWanted;
        private DateTime? lastShown;
        public bool IsBusy { get; private set; }
        public event Action<bool> FullscreenChanged;

        public AdService(IAdProvider provider, AdSettings settings, IClock clock, Func<bool> adsRemoved)
        { this.provider=provider; this.settings=settings; this.clock=clock; removed=adsRemoved; provider.Event+=OnEvent; }
        public void Initialize() => provider.Initialize();
        public bool IsReady(AdFormat format) => !disposed && !IsBusy && provider.IsReady(format);
        public bool CanShowInterstitial(int stage) => !disposed && !removed() && stage >= settings.firstInterstitialStage
            && (!lastShown.HasValue || (clock.UtcNow-lastShown.Value).TotalSeconds >= settings.interstitialCooldownSeconds);

        public UniTask<AdResult> ShowAsync(AdFormat format, int stage, string placement)
        {
            if (disposed) return UniTask.FromResult(AdResult.Cancelled);
            if (IsBusy) return UniTask.FromResult(AdResult.Busy);
            if (format == AdFormat.Interstitial && !CanShowInterstitial(stage)) return UniTask.FromResult(AdResult.Disabled);
            if (!provider.IsReady(format)) { provider.Load(format); return UniTask.FromResult(AdResult.NotReady); }
            IsBusy=true; earned=false; activeFormat=format;
            completion=new UniTaskCompletionSource<AdResult>();
            var task=completion.Task;
            timeout=new CancellationTokenSource();
            Watchdog(timeout.Token).Forget();
            FullscreenChanged?.Invoke(true);
            provider.SetBannerVisible(false);
            try { provider.Show(format, placement); }
            catch { Finish(AdResult.Failed); }
            return task;
        }
        private async UniTaskVoid Watchdog(CancellationToken token)
        {
            if (await UniTask.Delay(TimeSpan.FromSeconds(settings.showTimeoutSeconds), DelayType.Realtime, cancellationToken:token).SuppressCancellationThrow()) return;
            // Unblock the awaiting game, but quarantine new ads until the old native ad sends a terminal callback.
            // Otherwise a late reward/close can be mistaken for the next request.
            completion?.TrySetResult(AdResult.TimedOut);
            completion=null;
        }
        private void OnEvent(AdFormat format, AdEvent signal)
        {
            if (!IsBusy || format != activeFormat || disposed) return;
            switch(signal)
            {
                case AdEvent.Displayed: lastShown=clock.UtcNow; break; // Rewarded ads also reset the interstitial cooldown.
                case AdEvent.RewardEarned: if(format==AdFormat.Rewarded) earned=true; break;
                case AdEvent.Closed: Finish(format==AdFormat.Rewarded ? (earned?AdResult.Rewarded:AdResult.ClosedWithoutReward) : AdResult.Completed); break;
                case AdEvent.Failed: Finish(AdResult.Failed); break;
            }
        }
        private void Finish(AdResult result)
        {
            timeout?.Cancel(); timeout?.Dispose(); timeout=null;
            var done=completion; completion=null; IsBusy=false;
            FullscreenChanged?.Invoke(false);
            RefreshBanner();
            done?.TrySetResult(result);
        }
        public void SetBannerVisible(bool visible) { bannerWanted=visible; RefreshBanner(); }
        public void RefreshBanner() => provider.SetBannerVisible(!disposed && bannerWanted && !IsBusy && !removed());
        public void Dispose()
        {
            if(disposed)return;
            disposed=true; provider.Event-=OnEvent;
            if(IsBusy)Finish(AdResult.Cancelled);
            provider.Dispose();
        }
    }
}
