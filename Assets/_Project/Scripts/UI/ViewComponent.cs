using R3;
using UnityEngine;

namespace Project
{
    /// <summary>
    /// 뷰 프리팹 안에 놓이는 서브 컴포넌트. 부모 뷰가 바인딩하고, 구독은 파괴 시 함께 정리됩니다.
    /// 프레임워크 뷰(IUIView)가 아니므로 UIService 스택에는 오르지 않습니다.
    /// </summary>
    public abstract class ViewComponent : MonoBehaviour
    {
        private CompositeDisposable _disposables;

        protected CompositeDisposable Disposables => _disposables ??= new CompositeDisposable();

        /// <summary>재바인딩 전에 이전 구독을 끊습니다.</summary>
        protected void ClearSubscriptions()
        {
            _disposables?.Dispose();
            _disposables = null;
        }

        protected virtual void OnDestroy() => ClearSubscriptions();
    }
}
