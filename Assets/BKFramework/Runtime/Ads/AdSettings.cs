using System;
using UnityEngine;

namespace BK.Ads
{
    [Serializable]
    public sealed class AdUnits
    {
        public string rewarded, interstitial, banner;
    }

    [CreateAssetMenu(menuName = "BK/Ads/AppLovin Settings")]
    public sealed class AdSettings : ScriptableObject
    {
        [Tooltip("SDK Key is configured separately in AppLovin > Integration Manager. Enable after entering this app's IDs.")]
        public bool enableMobileAds;
        public bool simulateInEditor = true;
        public AdUnits android = new(), ios = new();
        public string[] testDeviceAdvertisingIds = Array.Empty<string>();
        public bool verboseLogging;
        [Min(1)] public int firstInterstitialStage = 12;
        [Min(0)] public float interstitialCooldownSeconds = 90;
        [Tooltip("0 disables the clear reward video button.")]
        [Min(0)] public int firstRewardStage = 12;
        public bool showLobbyBanner;
        [Min(30)] public float showTimeoutSeconds = 300;

        public AdUnits ActiveUnits
        {
            get
            {
#if UNITY_EDITOR
                // Local MAX editor stub IDs; never sent from a mobile player.
                return simulateInEditor ? new AdUnits { rewarded="bk_editor_rewarded", interstitial="bk_editor_interstitial", banner="bk_editor_banner" } : null;
#elif UNITY_ANDROID
                return enableMobileAds ? android : null;
#elif UNITY_IOS
                return enableMobileAds ? ios : null;
#else
                return null;
#endif
            }
        }
    }
}
