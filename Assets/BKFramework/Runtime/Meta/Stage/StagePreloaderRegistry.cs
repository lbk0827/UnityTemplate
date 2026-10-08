using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Core.Diagnostics;

namespace BK.Meta
{
    /// <summary>Implemented by the in-game core: warm the assets for a stage into the cache.</summary>
    public interface IStagePreloader
    {
        UniTask PreloadStageAsync(int stageNumber, CancellationToken cancellationToken);
    }

    public interface IStagePreloaderRegistry
    {
        /// <summary>Last registration wins.</summary>
        void Register(IStagePreloader preloader);

        /// <summary>Clears only when <paramref name="preloader"/> is the current one.</summary>
        void Unregister(IStagePreloader preloader);

        /// <summary>Fire-and-forget. Cancels any preload still in flight.</summary>
        void StartPreload(int stageNumber);

        bool IsPreloading { get; }
    }

    /// <inheritdoc cref="IStagePreloaderRegistry"/>
    public sealed class StagePreloaderRegistry : IStagePreloaderRegistry, IDisposable
    {
        private IStagePreloader _current;
        private CancellationTokenSource _inflight;
        private int _generation;
        private int _activeGeneration;

        public bool IsPreloading { get; private set; }

        public void Register(IStagePreloader preloader) => _current = preloader;

        public void Unregister(IStagePreloader preloader)
        {
            if (ReferenceEquals(_current, preloader))
                _current = null;
        }

        public void StartPreload(int stageNumber)
        {
            _inflight?.Cancel();
            _inflight?.Dispose();
            _inflight = new CancellationTokenSource();

            // Only the newest run may clear IsPreloading in its finally.
            var generation = ++_generation;
            _activeGeneration = generation;

            var preloader = _current;
            if (preloader == null)
            {
                BKLog.Warn(BKLog.Scene, $"no stage preloader registered; skipping stage {stageNumber}");
                return;
            }

            RunAsync(preloader, stageNumber, generation, _inflight.Token).Forget();
        }

        private async UniTaskVoid RunAsync(IStagePreloader preloader, int stageNumber, int generation, CancellationToken cancellationToken)
        {
            IsPreloading = true;
            try
            {
                await preloader.PreloadStageAsync(stageNumber, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer preload: expected, not logged.
            }
            catch (Exception exception)
            {
                BKLog.Error(BKLog.Scene, $"preload of stage {stageNumber} failed: {exception}");
            }
            finally
            {
                if (generation == _activeGeneration)
                    IsPreloading = false;
            }
        }

        public void Dispose()
        {
            _inflight?.Cancel();
            _inflight?.Dispose();
            _inflight = null;
        }
    }
}
