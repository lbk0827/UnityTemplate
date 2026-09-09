using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer.Unity;
using BK.Core.Diagnostics;
using BK.Core.Events;

namespace BK.Core.App
{
    /// <summary>Published once the boot sequence has finished successfully.</summary>
    public readonly struct AppBootCompleted { }

    /// <summary>Published when a boot step throws. The app is not usable after this.</summary>
    public readonly struct AppBootFailed
    {
        public readonly string StepName;
        public readonly Exception Exception;

        public AppBootFailed(string stepName, Exception exception)
        {
            StepName = stepName;
            Exception = exception;
        }
    }

    /// <summary>
    /// Drives the boot sequence from the root scope's entry point. Kept separate from
    /// <see cref="BootSequence"/> so the sequence itself stays testable without VContainer.
    /// </summary>
    public sealed class AppBootstrapper : IAsyncStartable
    {
        private readonly BootSequence _sequence;
        private readonly IEventBus _eventBus;
        private readonly IProgress<BootProgress> _progress;

        public AppBootstrapper(BootSequence sequence, IEventBus eventBus, IProgress<BootProgress> progress)
        {
            _sequence = sequence;
            _eventBus = eventBus;
            _progress = progress;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            BKLog.Info(BKLog.Boot, $"starting boot ({_sequence.StepCount} steps)");

            try
            {
                await _sequence.RunAsync(_progress, cancellation);
            }
            catch (OperationCanceledException)
            {
                // Play mode exited or the app is shutting down; not a failure.
                BKLog.Info(BKLog.Boot, "boot cancelled");
                return;
            }
            catch (BootFailedException failure)
            {
                BKLog.Exception(BKLog.Boot, failure);
                _eventBus.Publish(new AppBootFailed(failure.StepName, failure.InnerException));
                return;
            }

            _eventBus.Publish(new AppBootCompleted());
        }
    }
}
