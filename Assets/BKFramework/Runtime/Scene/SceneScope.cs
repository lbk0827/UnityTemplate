using System;
using Cysharp.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.AddressableAssets;
using VContainer;
using BK.Assets;
using BK.Core.Diagnostics;

namespace BK.Scene
{
    /// <summary>
    /// Binds three lifetimes that must end together: the loaded Unity scene, the DI
    /// child container, and the asset scope. Disposing tears down all three in reverse
    /// order of creation.
    /// </summary>
    internal sealed class SceneScope : ISceneScope
    {
        private readonly IObjectResolver _resolver;
        private readonly AsyncOperationHandle<SceneInstance> _sceneHandle;

        public SceneScope(
            string sceneName,
            AsyncOperationHandle<SceneInstance> sceneHandle,
            IObjectResolver resolver,
            IAssetScope assets)
        {
            SceneName = sceneName;
            _sceneHandle = sceneHandle;
            _resolver = resolver;
            Assets = assets;
        }

        public string SceneName { get; }
        public bool IsDisposed { get; private set; }
        public IAssetScope Assets { get; }

        public T Resolve<T>()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(SceneScope), $"Scene scope '{SceneName}' is disposed.");
            return _resolver.Resolve<T>();
        }

        /// <summary>
        /// Synchronous teardown of the container and assets. The Unity scene itself
        /// unloads asynchronously; <see cref="UnloadAsync"/> is what the service awaits.
        /// </summary>
        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;

            _resolver.Dispose();
            Assets.Dispose();
            BKLog.Verbose(BKLog.Scene, $"scope '{SceneName}' disposed");
        }

        /// <summary>Disposes the scope, then unloads the Unity scene.</summary>
        internal async UniTask UnloadAsync()
        {
            Dispose();

            if (_sceneHandle.IsValid())
                await Addressables.UnloadSceneAsync(_sceneHandle).ToUniTask();
        }
    }
}
