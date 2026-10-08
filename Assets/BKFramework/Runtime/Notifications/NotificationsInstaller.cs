using VContainer;

namespace BK.Notifications
{
    /// <summary>
    /// Registers the platform backend (Unity Mobile Notifications on device when the package
    /// is installed, otherwise a no-op) and the <see cref="LocalPushService"/>. Games register
    /// their <see cref="IPushProvider"/>s with <c>.As&lt;IPushProvider&gt;()</c>.
    /// </summary>
    public static class NotificationsInstaller
    {
        public static void Install(IContainerBuilder builder)
        {
#if BK_MOBILE_NOTIFICATIONS && (UNITY_ANDROID || UNITY_IOS)
            builder.Register<UnityMobileNotificationsPlatform>(Lifetime.Singleton).As<INotificationPlatform>();
#else
            builder.Register<NullNotificationPlatform>(Lifetime.Singleton).As<INotificationPlatform>();
#endif
            builder.Register<LocalPushService>(Lifetime.Singleton);
        }
    }
}
