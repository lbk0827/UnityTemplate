using Cysharp.Threading.Tasks;

namespace BK.UI
{
    /// <summary>Optional: a scene/core publishes "first frame is ready to show" so the cover lifts exactly then.</summary>
    public interface IRevealReady
    {
        bool IsReady { get; }
        UniTask ReadyTask { get; }
    }
}
