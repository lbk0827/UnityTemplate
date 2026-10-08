using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using BK.Core.Diagnostics;

namespace BK.UI
{
    /// <inheritdoc cref="IScreenCover"/>
    public sealed class ScreenCover : IScreenCover, IDisposable
    {
        private readonly ICoverView _view;
        private readonly float _fadeOutSeconds;
        private readonly float _readyTimeoutSeconds;
        private readonly Subject<Unit> _released = new();
        private IRevealReady _ready;
        private UniTaskCompletionSource _releaseSource;

        public ScreenCover(ICoverView view, float fadeOutSeconds = 0.15f, float readyTimeoutSeconds = 3f)
        {
            _view = view;
            _fadeOutSeconds = fadeOutSeconds;
            _readyTimeoutSeconds = readyTimeoutSeconds;
        }

        public bool IsActive { get; private set; }
        public Observable<Unit> Released => _released;

        public void RegisterRevealReady(IRevealReady ready) => _ready = ready;

        public void UnregisterRevealReady(IRevealReady ready)
        {
            if (ReferenceEquals(_ready, ready))
                _ready = null;
        }

        public void Hold()
        {
            _view.SetVisible(true);
            _view.SetAlpha(1f);
            if (IsActive)
                return;
            IsActive = true;
            _releaseSource = new UniTaskCompletionSource();
        }

        public async UniTask HoldAsync(float fadeInSeconds, CancellationToken cancellationToken = default)
        {
            if (IsActive)
                return;
            IsActive = true;
            _releaseSource = new UniTaskCompletionSource();
            _view.SetVisible(true);
            _view.SetAlpha(0f);
            await _view.FadeAsync(1f, fadeInSeconds, cancellationToken);
        }

        public async UniTask ReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (!IsActive)
                return;
            try { await _view.FadeAsync(0f, _fadeOutSeconds, cancellationToken); }
            finally { Finish(); }
        }

        public async UniTask ReleaseWhenReadyAsync(CancellationToken cancellationToken = default)
        {
            if (!IsActive)
                return;
            try
            {
                var ready = _ready;
                if (ready != null)
                {
                    // Player-loop timeout (not CancelAfter) so we never continue on a thread-pool thread.
                    var timeout = UniTask.Delay(TimeSpan.FromSeconds(_readyTimeoutSeconds), ignoreTimeScale: true, cancellationToken: cancellationToken);
                    var winner = await UniTask.WhenAny(ready.ReadyTask, timeout);
                    if (winner == 1)
                        BKLog.Warn(BKLog.UI, $"reveal ready timed out after {_readyTimeoutSeconds}s; releasing cover");
                }
                else
                {
                    // One frame so the new scene has rendered under the cover before we lift it.
                    await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
                }

                await _view.FadeAsync(0f, _fadeOutSeconds, cancellationToken);
            }
            finally { Finish(); }
        }

        public UniTask WaitUntilReleasedAsync(CancellationToken cancellationToken = default)
        {
            var source = _releaseSource;
            return !IsActive || source == null
                ? UniTask.CompletedTask
                : source.Task.AttachExternalCancellation(cancellationToken);
        }

        private void Finish()
        {
            if (!IsActive)
                return;
            _view.SetVisible(false);
            IsActive = false;
            _releaseSource?.TrySetResult();
            _releaseSource = null;
            _released.OnNext(Unit.Default);
        }

        public void Dispose()
        {
            _view.SetVisible(false);
            _released.Dispose();
        }
    }
}
