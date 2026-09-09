using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Project
{
    /// <summary>
    /// 상품 카드 공통. 가격/구매 버튼과 등장 연출(숨김 → 슬라이드 인)을 가집니다.
    /// 구매 실행은 상점 페이지가 소유하고, 카드는 클릭만 알립니다.
    /// </summary>
    public abstract class ProductCellBase : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _root;
        [SerializeField] private Button _buyButton;
        [SerializeField] private Text _priceText;
        [SerializeField] private float _appearOffset = 60f;
        [SerializeField] private float _appearDuration = 0.25f;

        private MotionHandle _fade;
        private MotionHandle _slide;
        private Vector2 _homePosition;

        public ShopProductRow Product { get; private set; }
        public event Action<ShopProductRow> BuyClicked;

        public void Bind(ShopProductRow product, Func<string, string> localize)
        {
            Product = product;
            _homePosition = _root.anchoredPosition;
            _priceText.text = $"${product.Price:0.00}";
            _buyButton.onClick.RemoveAllListeners();
            _buyButton.onClick.AddListener(() => BuyClicked?.Invoke(Product));
            OnBind(product, localize);
        }

        protected abstract void OnBind(ShopProductRow product, Func<string, string> localize);

        public void Hide()
        {
            CancelMotions();
            _canvasGroup.alpha = 0f;
            _root.anchoredPosition = _homePosition + new Vector2(0f, -_appearOffset);
        }

        public UniTask AppearAsync(float delay, CancellationToken cancellationToken)
        {
            CancelMotions();
            _fade = LMotion.Create(_canvasGroup.alpha, 1f, _appearDuration)
                .WithDelay(delay)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAlpha(_canvasGroup);
            _slide = LMotion.Create(_root.anchoredPosition, _homePosition, _appearDuration)
                .WithDelay(delay)
                .WithEase(Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .BindToAnchoredPosition(_root);
            return _slide.ToUniTask(cancellationToken);
        }

        public void ShowImmediate()
        {
            CancelMotions();
            _canvasGroup.alpha = 1f;
            _root.anchoredPosition = _homePosition;
        }

        private void CancelMotions()
        {
            if (_fade.IsActive()) _fade.Cancel();
            if (_slide.IsActive()) _slide.Cancel();
        }

        private void OnDestroy() => CancelMotions();
    }
}
