using Cysharp.Threading.Tasks;

namespace BK.UI
{
    public interface IDimScheduler
    {
        /// <summary>Short wait after the count hits zero so a popup chain does not flicker.</summary>
        UniTask GraceDelayAsync();

        /// <summary>Upper bound for a transition hold that never gets its next Acquire.</summary>
        UniTask TransitionTimeoutAsync();
    }
}
