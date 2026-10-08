#if BK_MOBILE_NOTIFICATIONS && (UNITY_ANDROID || UNITY_IOS)
using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#else
using Unity.Notifications.iOS;
#endif

namespace BK.Notifications
{
    /// <summary>Unity Mobile Notifications backend. Compiled only when the package is present and the target is Android or iOS.</summary>
    public sealed class UnityMobileNotificationsPlatform : INotificationPlatform
    {
        private const string ChannelId = "bk_default";
        private bool _channelReady;

        public bool IsSupported => !Application.isEditor;

#if UNITY_ANDROID
        public async UniTask<bool> RequestPermissionAsync()
        {
            if (Application.isEditor) return true;
            EnsureChannel();
            if (AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed) return true;
            var request = new PermissionRequest();
            while (request.Status == PermissionStatus.RequestPending)
                await UniTask.Yield();
            return request.Status == PermissionStatus.Allowed;
        }

        public void Schedule(NotificationRequest request)
        {
            if (Application.isEditor) return;
            EnsureChannel();
            var notification = new AndroidNotification
            {
                Title = request.Title,
                Text = request.Body,
                FireTime = request.FireAtLocal,
                ShowTimestamp = true,
            };
            AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, request.Id);
        }

        public void CancelAll()
        {
            if (Application.isEditor) return;
            AndroidNotificationCenter.CancelAllScheduledNotifications();
            AndroidNotificationCenter.CancelAllDisplayedNotifications();
        }

        private void EnsureChannel()
        {
            if (_channelReady) return;
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
            {
                Id = ChannelId,
                Name = "Default",
                Importance = Importance.Default,
                Description = "Game reminders",
            });
            _channelReady = true;
        }
#else
        public async UniTask<bool> RequestPermissionAsync()
        {
            if (Application.isEditor) return true;
            using var request = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, true);
            while (!request.IsFinished)
                await UniTask.Yield();
            return request.Granted;
        }

        public void Schedule(NotificationRequest request)
        {
            if (Application.isEditor) return;
            var fire = request.FireAtLocal;
            var notification = new iOSNotification
            {
                Identifier = request.Id.ToString(),
                Title = request.Title,
                Body = request.Body,
                ShowInForeground = false,
                Trigger = new iOSNotificationCalendarTrigger
                {
                    Year = fire.Year, Month = fire.Month, Day = fire.Day, Hour = fire.Hour, Minute = fire.Minute, Second = fire.Second,
                    Repeats = false,
                },
            };
            iOSNotificationCenter.ScheduleNotification(notification);
        }

        public void CancelAll()
        {
            if (Application.isEditor) return;
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
        }
#endif
    }
}
#endif
