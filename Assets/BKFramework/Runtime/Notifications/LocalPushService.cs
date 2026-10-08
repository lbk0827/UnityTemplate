using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using BK.Core.Diagnostics;
using BK.Core.Time;
using BK.Save;

namespace BK.Notifications
{
    /// <summary>
    /// sf's "queue, clear and reschedule" model: the OS holds notifications only while the app
    /// is in the background. On backgrounding every provider is asked for its requests and they
    /// are scheduled (if permission was granted); on foregrounding everything is cancelled.
    /// Permission is requested only after game code marks it pending (e.g. a few stages in).
    /// </summary>
    public sealed class LocalPushService : IDisposable
    {
        public const string Category = "Push";
        private static readonly TimeSpan MinimumLead = TimeSpan.FromSeconds(1);

        private readonly PushData _data;
        private readonly IClock _clock;
        private readonly INotificationPlatform _platform;
        private readonly List<IPushProvider> _providers = new();
        private LocalPushDriver _driver;
        private bool _requesting;

        public LocalPushService(ISaveService saves, IClock clock, INotificationPlatform platform, IEnumerable<IPushProvider> providers)
        {
            _data = saves.Get<PushData>();
            _clock = clock;
            _platform = platform;
            _providers.AddRange(providers);
            if (Application.isPlaying)
                _driver = LocalPushDriver.Create(this);
        }

        public bool PermissionRequested => _data.permissionRequested;
        public bool PermissionGranted => _data.permissionGranted;
        public bool IsPermissionPending => _data.permissionPending && !_data.permissionRequested;
        public IReadOnlyList<IPushProvider> Providers => _providers;

        /// <summary>Last background pass, for diagnostics and tests.</summary>
        public IReadOnlyList<NotificationRequest> LastScheduled { get; private set; } = Array.Empty<NotificationRequest>();

        /// <summary>Game code: the player has progressed enough to be asked. No-op once the prompt was shown.</summary>
        public void MarkPendingPermissionRequest()
        {
            if (_data.permissionRequested || _data.permissionPending)
                return;
            _data.permissionPending = true;
            _data.MarkDirty();
        }

        /// <summary>Shows the OS prompt when one is pending. Records that it was shown whatever the answer.</summary>
        public async UniTask<bool> TryRequestPendingPermissionAsync()
        {
            if (!IsPermissionPending || _requesting)
                return _data.permissionGranted;
            _requesting = true;
            try
            {
                var granted = await _platform.RequestPermissionAsync();
                _data.permissionRequested = true;
                _data.permissionPending = false;
                _data.permissionGranted = granted;
                _data.MarkDirty();
                BKLog.Info(Category, granted ? "permission granted" : "permission denied");
                return granted;
            }
            finally
            {
                _requesting = false;
            }
        }

        /// <summary>Clears the OS queue and, with permission, schedules every provider's requests. Returns the count scheduled.</summary>
        public int OnBackgrounded()
        {
            _platform.CancelAll();
            if (!_platform.IsSupported || !_data.permissionGranted)
            {
                LastScheduled = Array.Empty<NotificationRequest>();
                return 0;
            }

            var nowLocal = _clock.UtcNow.ToLocalTime();
            var scheduled = new List<NotificationRequest>();
            foreach (var provider in _providers)
            {
                IEnumerable<NotificationRequest> requests;
                try { requests = provider.Build(nowLocal); }
                catch (Exception exception)
                {
                    BKLog.Error(Category, $"{provider.GetType().Name} failed: {exception.Message}");
                    continue;
                }

                foreach (var request in requests)
                {
                    if (request.FireAtLocal <= nowLocal + MinimumLead)
                        continue; // would fire immediately or in the past: pointless noise
                    _platform.Schedule(request);
                    scheduled.Add(request);
                }
            }

            LastScheduled = scheduled;
            BKLog.Info(Category, $"scheduled {scheduled.Count} notifications");
            return scheduled.Count;
        }

        /// <summary>Back in the app: nothing should remain pending in the OS.</summary>
        public void OnForegrounded()
        {
            _platform.CancelAll();
            LastScheduled = Array.Empty<NotificationRequest>();
        }

        public void Dispose()
        {
            if (_driver != null)
                UnityEngine.Object.Destroy(_driver.gameObject);
        }
    }

    /// <summary>Relays pause/focus to the service. Created by the service in play mode.</summary>
    [AddComponentMenu("")]
    internal sealed class LocalPushDriver : MonoBehaviour
    {
        private LocalPushService _service;
        private bool _inBackground;

        public static LocalPushDriver Create(LocalPushService service)
        {
            var go = new GameObject("[BK.Notifications]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            var driver = go.AddComponent<LocalPushDriver>();
            driver._service = service;
            return driver;
        }

        private void OnApplicationPause(bool paused) => Transition(paused);
        private void OnApplicationFocus(bool focused) { if (focused) Transition(false); }

        private void Transition(bool toBackground)
        {
            if (toBackground == _inBackground)
                return;
            _inBackground = toBackground;
            if (toBackground) _service.OnBackgrounded();
            else _service.OnForegrounded();
        }
    }
}
