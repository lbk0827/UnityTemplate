using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Core.Diagnostics;

namespace BK.Core.App
{
    /// <summary>
    /// Runs every registered <see cref="IBootStep"/> once, in ascending order.
    /// Steps run sequentially: a step may depend on services initialised by an
    /// earlier one, and ordering is the only contract they have to reason about.
    /// </summary>
    public sealed class BootSequence
    {
        private readonly IBootStep[] _steps;
        private bool _hasRun;

        public BootSequence(IEnumerable<IBootStep> steps)
        {
            var ordered = new List<IBootStep>(steps);
            ordered.Sort(static (a, b) => a.Order.CompareTo(b.Order));
            _steps = ordered.ToArray();
        }

        public int StepCount => _steps.Length;

        /// <exception cref="BootFailedException">
        /// A step threw. The original exception is preserved as the inner exception so
        /// the failing step is identifiable without unwinding the whole boot.
        /// </exception>
        public async UniTask RunAsync(IProgress<BootProgress> progress, CancellationToken cancellationToken)
        {
            if (_hasRun)
                throw new InvalidOperationException("BootSequence has already run.");
            _hasRun = true;

            progress?.Report(new BootProgress(0, _steps.Length, _steps.Length > 0 ? _steps[0].Name : string.Empty));

            for (var i = 0; i < _steps.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var step = _steps[i];
                BKLog.Info(BKLog.Boot, $"step {i + 1}/{_steps.Length}: {step.Name}");

                try
                {
                    await step.ExecuteAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new BootFailedException(step.Name, exception);
                }

                var next = i + 1 < _steps.Length ? _steps[i + 1].Name : string.Empty;
                progress?.Report(new BootProgress(i + 1, _steps.Length, next));
            }

            BKLog.Info(BKLog.Boot, "boot sequence complete");
        }
    }

    public sealed class BootFailedException : Exception
    {
        public string StepName { get; }

        public BootFailedException(string stepName, Exception innerException)
            : base($"Boot step '{stepName}' failed.", innerException)
            => StepName = stepName;
    }
}
