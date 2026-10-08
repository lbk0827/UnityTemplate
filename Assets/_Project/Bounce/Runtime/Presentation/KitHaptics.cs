using UnityEngine;

namespace BK.Kit
{
    public static class KitHaptics
    {
        private static float nextPulse;
        public static void Pulse()
        {
            if(KitApp.Instance==null || !KitApp.Instance.Progress.hapticsEnabled || Time.unscaledTime<nextPulse)return;
            nextPulse=Time.unscaledTime+.3f;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }
    }
}
