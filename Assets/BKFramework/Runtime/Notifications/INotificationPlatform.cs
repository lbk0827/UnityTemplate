using Cysharp.Threading.Tasks;

namespace BK.Notifications
{
    /// <summary>The OS side of local notifications. Only <see cref="LocalPushService"/> talks to it.</summary>
    public interface INotificationPlatform
    {
        /// <summary>False in the editor and on platforms without a backend; scheduling is then a no-op.</summary>
        bool IsSupported { get; }

        UniTask<bool> RequestPermissionAsync();

        void Schedule(NotificationRequest request);

        void CancelAll();
    }

    /// <summary>Editor / unsupported platform: grants permission, schedules nothing.</summary>
    public sealed class NullNotificationPlatform : INotificationPlatform
    {
        public bool IsSupported => false;
        public UniTask<bool> RequestPermissionAsync() => UniTask.FromResult(true);
        public void Schedule(NotificationRequest request) { }
        public void CancelAll() { }
    }
}
