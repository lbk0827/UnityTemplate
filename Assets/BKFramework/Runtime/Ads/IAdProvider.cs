using System;

namespace BK.Ads
{
    public enum AdFormat { Rewarded, Interstitial }
    public enum AdEvent { Displayed, RewardEarned, Closed, Failed }
    public enum AdResult { Completed, Rewarded, ClosedWithoutReward, NotReady, Disabled, Busy, Failed, TimedOut, Cancelled }

    public interface IAdProvider : IDisposable
    {
        event Action<AdFormat, AdEvent> Event;
        void Initialize();
        bool IsReady(AdFormat format);
        void Load(AdFormat format);
        void Show(AdFormat format, string placement);
        void SetBannerVisible(bool visible);
    }
}
