using System;
using R3;

namespace BK.UI
{
    /// <inheritdoc cref="IPopupDim"/>
    public sealed class PopupDim : IPopupDim, IDisposable
    {
        private readonly ReactiveProperty<bool> _isActive = new(false);
        private readonly DimRefCounter _counter;

        [VContainer.Inject]
        public PopupDim(UIRoot root)
            : this(PopupDimView.Create(root.GetAuxiliaryRoot(UIRoot.DimCanvas, (int)UILayer.Popup - 1)), new UniTaskDimScheduler()) { }

        internal PopupDim(IDimView view, IDimScheduler scheduler)
        {
            _counter = new DimRefCounter(new Notifying(view, _isActive), scheduler);
        }

        public ReadOnlyReactiveProperty<bool> IsActive => _isActive;
        public void Acquire(DimLevel level = DimLevel.Soft) => _counter.Acquire(level);
        public void Release() => _counter.Release();
        public void HoldForTransition() => _counter.HoldForTransition();
        public void Reset() => _counter.Reset();
        public void Dispose() => _isActive.Dispose();

        private sealed class Notifying : IDimView
        {
            private readonly IDimView _inner;
            private readonly ReactiveProperty<bool> _active;

            public Notifying(IDimView inner, ReactiveProperty<bool> active)
            {
                _inner = inner;
                _active = active;
            }

            public void Show() { _active.Value = true; _inner.Show(); }
            public void Hide() { _active.Value = false; _inner.Hide(); }
            public void ApplyLevel(DimLevel level) => _inner.ApplyLevel(level);
        }
    }
}
