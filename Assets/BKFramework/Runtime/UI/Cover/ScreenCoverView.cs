using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BK.UI
{
    /// <summary>Runtime-built opaque black cover on the Cover canvas (above Popup/Overlay, below System).</summary>
    [RequireComponent(typeof(CanvasGroup), typeof(Image))]
    public sealed class ScreenCoverView : MonoBehaviour, ICoverView
    {
        private CanvasGroup _group;

        public static ScreenCoverView Create(UIRoot root)
        {
            var parent = root.GetAuxiliaryRoot(UIRoot.CoverCanvas, (int)UILayer.System - 50);
            var go = new GameObject("ScreenCover", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(ScreenCoverView));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            var view = go.GetComponent<ScreenCoverView>();
            view._group = go.GetComponent<CanvasGroup>();
            go.SetActive(false);
            return view;
        }

        public void SetVisible(bool visible)
        {
            if (this != null)
                gameObject.SetActive(visible);
        }

        public void SetAlpha(float alpha)
        {
            if (this != null)
                _group.alpha = alpha;
        }

        public async UniTask FadeAsync(float to, float seconds, CancellationToken cancellationToken)
        {
            if (this == null)
                return;
            var from = _group.alpha;
            try
            {
                // One rendered frame first so the covered scene has drawn before we start lifting.
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                var elapsed = 0f;
                while (elapsed < seconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Cut the fade short; the caller's finally settles visibility.
            }

            if (this != null)
                _group.alpha = to;
        }
    }
}
