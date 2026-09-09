using System.Threading;
using Cysharp.Threading.Tasks;
using LitMotion;
using LitMotion.Extensions;
using R3;
using UnityEngine;
using VContainer;

namespace Project
{
    /// <summary>
    /// 로비 본문. 상점/홈 페이지를 가로로 나란히 두고 탭에 맞춰 슬라이드합니다.
    /// 페이지 순서는 LobbyTab 열거형 값과 같습니다(Store=0, Home=1).
    /// </summary>
    public sealed class LobbyPanelView : ProjectViewBase
    {
        public const string Address = "UI/Lobby/LobbyPanel";

        [SerializeField] private RectTransform _pageContent;
        [SerializeField] private HomePageView _home;
        [SerializeField] private StorePageView _store;
        [SerializeField] private float _slideDuration = 0.25f;

        private LobbyState _lobby;
        private MotionHandle _slide;

        [Inject]
        public void Construct(LobbyState lobby) => _lobby = lobby;

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            _home.Bind(this);
            _store.Bind(this);

            SnapTo(_lobby.CurrentTab.Value);
            // 카드는 상점 탭에 "도착"했을 때 등장합니다. 그 전까지는 숨겨 둡니다.
            _store.HideAppearItems();
            _lobby.CurrentTab.Skip(1).Subscribe(OnTabChanged).AddTo(Disposables);
            return base.OnInitializeAsync(cancellationToken);
        }

        protected override async UniTask PlayOpenAsync(CancellationToken cancellationToken)
        {
            // 초기화 시점에는 캔버스 폭이 아직 0 일 수 있어 한 번 더 맞춥니다.
            SnapTo(_lobby.CurrentTab.Value);
            await base.PlayOpenAsync(cancellationToken);
            if (_lobby.CurrentTab.Value == LobbyTab.Store)
                _store.PlayAppear();
        }

        private void OnTabChanged(LobbyTab tab)
        {
            if (_slide.IsActive())
                _slide.Cancel();

            _store.HideAppearItems();

            _slide = LMotion.Create(_pageContent.anchoredPosition, TargetPosition(tab), _slideDuration)
                .WithEase(Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() =>
                {
                    if (tab == LobbyTab.Store)
                        _store.PlayAppear();
                })
                .BindToAnchoredPosition(_pageContent);
        }

        private void SnapTo(LobbyTab tab) => _pageContent.anchoredPosition = TargetPosition(tab);

        private Vector2 TargetPosition(LobbyTab tab)
        {
            var pageWidth = ((RectTransform)_pageContent.parent).rect.width;
            return new Vector2(-pageWidth * (int)tab, 0f);
        }

        protected override void OnDestroy()
        {
            if (_slide.IsActive())
                _slide.Cancel();
            base.OnDestroy();
        }
    }
}
