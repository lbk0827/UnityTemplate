using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BK.Kit
{
    // Independent replay of serialized visual keyframes. No external tween framework.
    public sealed class VisualSequence : MonoBehaviour
    {
        [Serializable] public sealed class Track
        {
            public UnityEngine.Object target;
            public int kind;
            public float start, duration;
            public Vector3 from, to;
            public bool reverse, relative;
            public AnimationCurve curve;
        }
        public Track[] tracks = Array.Empty<Track>();
        public bool autoplay, repeat;
        private Coroutine running;
        private void OnEnable() { if (autoplay) Play(); }
        private void OnDisable() { if (running != null) StopCoroutine(running); running = null; }
        public void Play()
        {
            if (!isActiveAndEnabled) return;
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Replay());
        }
        public void Finish() { foreach (var track in tracks) Apply(track, 1); }
        private IEnumerator Replay()
        {
            float duration = 0;
            foreach (var track in tracks) duration = Mathf.Max(duration, track.start + track.duration);
            do
            {
                float time = 0;
                while (time < duration)
                {
                    foreach (var track in tracks)
                        if (time >= track.start) Apply(track, Mathf.Clamp01((time - track.start) / Mathf.Max(.001f, track.duration)));
                    time += Time.unscaledDeltaTime;
                    yield return null;
                }
                Finish();
                yield return null;
            } while (repeat);
            running = null;
        }
        private static void Apply(Track track, float progress)
        {
            var go = track.target as GameObject;
            if (track.target is Component component) go = component.gameObject;
            if (go == null) return;
            float t = track.curve != null && track.curve.length > 0 ? track.curve.Evaluate(progress) : progress;
            var a = track.reverse ? track.to : track.from;
            var b = track.reverse ? track.from : track.to;
            Vector3 value = Vector3.LerpUnclamped(a, b, t);
            switch (track.kind)
            {
                case 0: go.transform.localScale = value; break;
                case 1: go.transform.localPosition = value; break;
                case 2: go.transform.localEulerAngles = value; break;
                case 3: if (go.TryGetComponent<CanvasGroup>(out var group)) group.alpha = value.x; break;
                case 4: if (go.TryGetComponent<Graphic>(out var graphic)) { var color = graphic.color; color.a = value.x; graphic.color = color; } break;
            }
        }
    }
}
