using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <summary>
    /// Pure dim lifetime: ref-count, transition bridge, deferred hide, reset.
    /// A generation counter invalidates in-flight async hides on re-acquire/reset.
    /// </summary>
    public sealed class DimRefCounter
    {
        private readonly IDimView _view;
        private readonly IDimScheduler _scheduler;
        private int _count, _gen;
        private bool _pending, _visible;
        private DimLevel _level = DimLevel.Soft;

        public DimRefCounter(IDimView view, IDimScheduler scheduler)
        {
            _view = view;
            _scheduler = scheduler;
        }

        public int Count => _count;
        public bool IsVisible => _visible;

        /// <summary>Most recent deferred hide/timeout, for tests. Completed when idle.</summary>
        public UniTask Pending { get; private set; } = UniTask.CompletedTask;

        public void Acquire(DimLevel level)
        {
            if (level == DimLevel.None)
                return;
            _level = level;
            _pending = false;
            _gen++;
            _count++;
            if (!_visible)
            {
                _visible = true;
                _view.ApplyLevel(_level);
                _view.Show();
            }
            else
            {
                _view.ApplyLevel(_level);
            }
        }

        public void Release()
        {
            if (_count <= 0)
                return;
            if (--_count == 0 && !_pending)
                Pending = HideAfterGraceAsync(_gen);
        }

        public void HoldForTransition()
        {
            _pending = true;
            Pending = WatchTimeoutAsync(++_gen);
        }

        public void Reset()
        {
            _count = 0;
            _pending = false;
            _gen++;
            _level = DimLevel.Soft;
            if (_visible)
            {
                _visible = false;
                _view.Hide();
            }
        }

        private async UniTask HideAfterGraceAsync(int gen)
        {
            await _scheduler.GraceDelayAsync();
            if (_count == 0 && !_pending && gen == _gen && _visible)
            {
                _visible = false;
                _view.Hide();
                _level = DimLevel.Soft;
            }
        }

        private async UniTask WatchTimeoutAsync(int gen)
        {
            await _scheduler.TransitionTimeoutAsync();
            if (gen != _gen)
                return;
            _pending = false;
            if (_count == 0 && _visible)
            {
                _visible = false;
                _view.Hide();
            }
        }
    }
}
