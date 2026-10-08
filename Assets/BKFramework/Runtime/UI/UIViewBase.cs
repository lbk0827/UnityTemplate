using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BK.UI
{
    /// <summary>
    /// Base for prefab-authored views. Supplies a CanvasGroup fade so a view is usable
    /// with no transition code; override the Async hooks for anything richer.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIViewBase : MonoBehaviour, IUIView, IDimmedView
    {
        [SerializeField] private UILayer _layer = UILayer.Content;
        [SerializeField] private float _fadeDuration = 0.15f;
        [SerializeField, Tooltip("Popup dim acquired while this view is open. None = no dim.")]
        private DimLevel _dim = DimLevel.None;

        private CanvasGroup _canvasGroup;

        public UILayer Layer => _layer;
        public DimLevel DimLevel => _dim;
        public bool IsOpen { get; private set; }

        /// <summary>For editor generators that author views in code.</summary>
        public void SetLayer(UILayer layer) => _layer = layer;
        public void SetDimLevel(DimLevel level) => _dim = level;

        protected CanvasGroup CanvasGroup
            => _canvasGroup ??= GetComponent<CanvasGroup>();

        public virtual UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            CanvasGroup.alpha = 0f;
            SetInteractable(false);
            return UniTask.CompletedTask;
        }

        public async UniTask OnOpenAsync(CancellationToken cancellationToken)
        {
            await PlayOpenAsync(cancellationToken);
            IsOpen = true;
            SetInteractable(true);
        }

        public async UniTask OnCloseAsync(CancellationToken cancellationToken)
        {
            // Drop interaction first so input during the hide transition cannot
            // re-enter a view that is on its way out.
            SetInteractable(false);
            IsOpen = false;
            await PlayCloseAsync(cancellationToken);
        }

        public virtual bool OnBackRequested() => false;

        protected virtual UniTask PlayOpenAsync(CancellationToken cancellationToken)
            => FadeAsync(CanvasGroup.alpha, 1f, cancellationToken);

        protected virtual UniTask PlayCloseAsync(CancellationToken cancellationToken)
            => FadeAsync(CanvasGroup.alpha, 0f, cancellationToken);

        protected async UniTask FadeAsync(float from, float to, CancellationToken cancellationToken)
        {
            if (_fadeDuration <= 0f)
            {
                CanvasGroup.alpha = to;
                return;
            }

            var elapsed = 0f;
            while (elapsed < _fadeDuration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                elapsed += Time.unscaledDeltaTime;
                CanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / _fadeDuration));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            CanvasGroup.alpha = to;
        }

        private void SetInteractable(bool value)
        {
            CanvasGroup.interactable = value;
            CanvasGroup.blocksRaycasts = value;
        }
    }
}
