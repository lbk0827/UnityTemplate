using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Assets;
using BK.Scene;

namespace BK.UI
{
    /// <summary>Cover → close content views → scene transition → reveal, in one call.</summary>
    public interface ISceneFlow
    {
        bool IsBusy { get; }
        UniTask<ISceneScope> TransitionAsync(AssetKey scene, CancellationToken cancellationToken = default);
    }
}
