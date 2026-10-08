using UnityEngine;

namespace BK.Save
{
    /// <summary>
    /// Play-mode only helper that flushes dirty slots on an interval, on pause and on
    /// quit. Created by <see cref="SaveService"/>; never add it to a scene by hand.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class SaveFlushDriver : MonoBehaviour
    {
        private SaveService _service;
        private float _nextFlush;

        public static SaveFlushDriver Create(SaveService service)
        {
            var go = new GameObject("[BK.Save]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            var driver = go.AddComponent<SaveFlushDriver>();
            driver._service = service;
            driver._nextFlush = Time.unscaledTime + service.FlushIntervalSeconds;
            return driver;
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextFlush) return;
            _nextFlush = Time.unscaledTime + _service.FlushIntervalSeconds;
            if (_service.HasUnsavedChanges) _service.Flush();
        }

        private void OnApplicationPause(bool paused) { if (paused) _service.Flush(); }
        private void OnApplicationQuit() => _service.Flush();
    }
}
