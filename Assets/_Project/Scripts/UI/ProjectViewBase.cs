using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;
using BK.Localization;
using BK.UI;

namespace Project
{
    /// <summary>
    /// 프로젝트 뷰 공통. 구독 수명, 로컬라이제이션, UI 서비스 접근을 한곳에 둡니다.
    /// 언어가 바뀌면 <see cref="ApplyTexts"/> 가 다시 불립니다.
    /// </summary>
    public abstract class ProjectViewBase : UIViewBase
    {
        private CompositeDisposable _disposables;

        protected CompositeDisposable Disposables => _disposables ??= new CompositeDisposable();
        protected IUIService UI { get; private set; }
        protected ILocalizationService Loc { get; private set; }

        [Inject]
        public void ConstructBase(IUIService ui, ILocalizationService loc)
        {
            UI = ui;
            Loc = loc;
        }

        public override UniTask OnInitializeAsync(CancellationToken cancellationToken)
        {
            Loc.CurrentLanguage.Subscribe(_ => ApplyTexts()).AddTo(Disposables);
            return base.OnInitializeAsync(cancellationToken);
        }

        /// <summary>정적 텍스트를 현재 언어로 채웁니다. 초기화 시 1회, 언어 변경 시마다.</summary>
        protected virtual void ApplyTexts() { }

        protected UniTask CloseAsync() => UI.CloseAsync(this);

        protected virtual void OnDestroy()
        {
            _disposables?.Dispose();
            _disposables = null;
        }
    }
}
