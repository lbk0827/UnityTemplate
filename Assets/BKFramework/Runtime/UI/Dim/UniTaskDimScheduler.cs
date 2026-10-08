using System;
using Cysharp.Threading.Tasks;

namespace BK.UI
{
    public sealed class UniTaskDimScheduler : IDimScheduler
    {
        private readonly int _graceFrames;
        private readonly float _timeoutSeconds;

        public UniTaskDimScheduler(int graceFrames = 1, float timeoutSeconds = 2f)
        {
            _graceFrames = graceFrames < 1 ? 1 : graceFrames;
            _timeoutSeconds = timeoutSeconds;
        }

        public UniTask GraceDelayAsync() => UniTask.DelayFrame(_graceFrames, PlayerLoopTiming.Update);

        public UniTask TransitionTimeoutAsync()
            => UniTask.Delay(TimeSpan.FromSeconds(_timeoutSeconds), ignoreTimeScale: true);
    }
}
