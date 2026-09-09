using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Assets;

namespace BK.Scene
{
    /// <summary>
    /// Loads scenes through Addressables and pairs each with an <see cref="ISceneScope"/>.
    /// Only one scene is "active" at a time; additive scenes are tracked separately
    /// so an active-scene transition does not tear down persistent additive content.
    /// </summary>
    public interface ISceneService
    {
        /// <summary>Currently active scene scope, or null before the first load.</summary>
        ISceneScope ActiveScene { get; }

        /// <summary>True while a transition is in flight. Guards against re-entrant loads.</summary>
        bool IsTransitioning { get; }

        /// <summary>
        /// Unloads the current active scene and loads <paramref name="key"/> in its place.
        /// </summary>
        UniTask<ISceneScope> TransitionToAsync(
            AssetKey key,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>Loads a scene additively. The caller owns the returned scope.</summary>
        UniTask<ISceneScope> LoadAdditiveAsync(
            AssetKey key,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);

        UniTask UnloadAsync(ISceneScope scope, CancellationToken cancellationToken = default);
    }
}
