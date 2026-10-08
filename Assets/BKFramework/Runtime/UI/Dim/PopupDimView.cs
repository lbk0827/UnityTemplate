using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BK.UI
{
    /// <summary>
    /// Full-screen black image whose darkness is the Image alpha and whose fade is the
    /// CanvasGroup alpha. Built at runtime; the framework ships no prefab for it.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup), typeof(Image))]
    public sealed class PopupDimView : MonoBehaviour, IDimView
    {
        public float FadeInSeconds = 0.2f;
        public float FadeOutSeconds = 0.2f;
        public float LevelTweenSeconds = 0.15f;

        private CanvasGroup _group;
        private Image _image;
        private CancellationTokenSource _fadeCts, _levelCts;

        public static PopupDimView Create(Transform parent)
        {
            var go = new GameObject("PopupDim", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(PopupDimView));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var view = go.GetComponent<PopupDimView>();
            view._group = go.GetComponent<CanvasGroup>();
            view._image = go.GetComponent<Image>();
            view._image.color = new Color(0f, 0f, 0f, AlphaFor(DimLevel.Soft));
            view._image.raycastTarget = true;
            view._group.alpha = 0f;
            go.SetActive(false);
            return view;
        }

        public static float AlphaFor(DimLevel level) => level switch
        {
            DimLevel.Full => 1f,
            DimLevel.Deeper => 0.99f,
            DimLevel.Deep => 0.95f,
            DimLevel.Medium => 0.90f,
            DimLevel.Soft => 0.85f,
            _ => 0f,
        };

        public void ApplyLevel(DimLevel level)
        {
            if (this == null)
                return;
            var target = AlphaFor(level);
            Cancel(ref _levelCts);
            if (!gameObject.activeSelf || LevelTweenSeconds <= 0f)
            {
                _image.color = new Color(0f, 0f, 0f, target);
                return;
            }

            _levelCts = new CancellationTokenSource();
            TweenAsync(_image.color.a, target, LevelTweenSeconds, a => _image.color = new Color(0f, 0f, 0f, a), _levelCts.Token).Forget();
        }

        public void Show() => Fade(1f, FadeInSeconds, activate: true);
        public void Hide() => Fade(0f, FadeOutSeconds, activate: false);

        private void Fade(float target, float seconds, bool activate)
        {
            if (this == null)
                return;
            Cancel(ref _fadeCts);
            if (activate && !gameObject.activeSelf)
                gameObject.SetActive(true);

            _fadeCts = new CancellationTokenSource();
            var token = _fadeCts.Token;
            TweenAsync(_group.alpha, target, seconds, a => _group.alpha = a, token)
                .ContinueWith(() =>
                {
                    if (target <= 0f && !token.IsCancellationRequested && this != null)
                        gameObject.SetActive(false);
                })
                .Forget();
        }

        private static async UniTask TweenAsync(float from, float to, float seconds, Action<float> apply, CancellationToken ct)
        {
            try
            {
                var elapsed = 0f;
                while (elapsed < seconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds))));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                apply(to);
            }
            catch (OperationCanceledException)
            {
                // Replaced by a newer tween; it owns the final state.
            }
        }

        private static void Cancel(ref CancellationTokenSource cts)
        {
            if (cts == null)
                return;
            try { cts.Cancel(); }
            catch (ObjectDisposedException) { }
            cts.Dispose();
            cts = null;
        }

        private void OnDestroy()
        {
            Cancel(ref _fadeCts);
            Cancel(ref _levelCts);
        }
    }
}
