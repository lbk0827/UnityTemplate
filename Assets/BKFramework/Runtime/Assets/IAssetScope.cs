using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BK.Assets
{
    /// <summary>
    /// Owns a set of loaded assets. Disposing releases every handle the scope
    /// acquired, which is what keeps asset lifetime tied to scene/UI lifetime
    /// instead of relying on callers to pair every load with a release.
    /// </summary>
    public interface IAssetScope : IDisposable
    {
        string Name { get; }
        bool IsDisposed { get; }

        UniTask<T> LoadAsync<T>(AssetKey key, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        UniTask<GameObject> InstantiateAsync(
            AssetKey key,
            Transform parent = null,
            CancellationToken cancellationToken = default);

        /// <summary>Destroys an instance created by this scope and drops its handle.</summary>
        void ReleaseInstance(GameObject instance);
    }
}
