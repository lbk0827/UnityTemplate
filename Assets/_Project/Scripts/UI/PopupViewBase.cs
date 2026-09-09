using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;
using BK.Presentation;

namespace Project
{
    /// <summary>
    /// 팝업 공통. 닫기 버튼 배열이 자동으로 닫기에 배선되고, 패널이 스케일 팝으로 열리고 닫힙니다.
    /// 뒤로가기는 기본적으로 팝업을 닫습니다.
    /// </summary>
    public abstract class PopupViewBase : ProjectViewBase
    {
        [SerializeField, Tooltip("클릭하면 팝업을 닫는 버튼들(X, 배경 딤 등)")]
        private Button[] _closeButtons;
        [SerializeField, Tooltip("스케일 연출 대상. 비우면 페이드만")]
        private RectTransform _panel;
        [SerializeField] private float _popDuration = 0.2f;

        private bool _closing;

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            if (_closeButtons != null)
                foreach (var button in _closeButtons)
                    if (button != null)
                        button.onClick.AddListener(RequestClose);

            return base.OnInitializeAsync(cancellationToken);
        }

        /// <summary>같은 팝업에 닫기가 두 번 들어와도 한 번만 처리합니다.</summary>
        protected void RequestClose()
        {
            if (_closing || !IsOpen)
                return;
            _closing = true;
            CloseAsync().Forget();
        }

        protected override UniTask PlayOpenAsync(CancellationToken cancellationToken)
        {
            if (_panel == null)
                return base.PlayOpenAsync(cancellationToken);

            _panel.localScale = Vector3.one * 0.85f;
            return UniTask.WhenAll(
                base.PlayOpenAsync(cancellationToken),
                Motions.ScaleAsync(_panel, Vector3.one, _popDuration, Ease.OutBack, cancellationToken));
        }

        protected override UniTask PlayCloseAsync(CancellationToken cancellationToken)
        {
            if (_panel == null)
                return base.PlayCloseAsync(cancellationToken);

            return UniTask.WhenAll(
                base.PlayCloseAsync(cancellationToken),
                Motions.ScaleAsync(_panel, Vector3.one * 0.85f, _popDuration * 0.6f, Ease.InQuad, cancellationToken));
        }
    }
}
