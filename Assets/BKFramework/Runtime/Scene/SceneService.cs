using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using VContainer;
using BK.Assets;
using BK.Core.Diagnostics;

namespace BK.Scene
{
    /// <inheritdoc cref="ISceneService"/>
    public sealed class SceneService : ISceneService, IDisposable
    {
        private readonly IObjectResolver _rootResolver;
        private readonly IAssetService _assetService;
        private readonly List<SceneScope> _additiveScopes = new();

        private SceneScope _activeScope;

        public SceneService(IObjectResolver rootResolver, IAssetService assetService)
        {
            _rootResolver = rootResolver;
            _assetService = assetService;
        }

        public ISceneScope ActiveScene => _activeScope;
        public bool IsTransitioning { get; private set; }

        public async UniTask<ISceneScope> TransitionToAsync(
            AssetKey key,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (IsTransitioning)
                throw new InvalidOperationException(
                    $"A scene transition is already running; cannot start '{key}'.");

            IsTransitioning = true;
            try
            {
                // Load additively first so we never sit on an empty scene graph, then
                // drop the outgoing scene once the incoming one is live.
                var outgoing = _activeScope;
                var incoming = await LoadAsync(key, LoadSceneMode.Additive, progress, cancellationToken);

                if (outgoing != null)
                    await outgoing.UnloadAsync();

                SceneManager.SetActiveScene(SceneManager.GetSceneByName(incoming.SceneName));
                _activeScope = incoming;
                return incoming;
            }
            finally
            {
                IsTransitioning = false;
            }
        }

        public async UniTask<ISceneScope> LoadAdditiveAsync(
            AssetKey key,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            var scope = await LoadAsync(key, LoadSceneMode.Additive, progress, cancellationToken);
            _additiveScopes.Add(scope);
            return scope;
        }

        public async UniTask UnloadAsync(ISceneScope scope, CancellationToken cancellationToken = default)
        {
            if (scope is not SceneScope concrete)
                throw new ArgumentException("Scope was not created by this service.", nameof(scope));

            _additiveScopes.Remove(concrete);
            if (ReferenceEquals(_activeScope, concrete))
                _activeScope = null;

            await concrete.UnloadAsync();
        }

        private async UniTask<SceneScope> LoadAsync(
            AssetKey key,
            LoadSceneMode mode,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (!key.IsValid)
                throw new ArgumentException("Scene key is empty.", nameof(key));

            BKLog.Info(BKLog.Scene, $"loading '{key}'");

            var handle = Addressables.LoadSceneAsync(key.Address, mode);
            SceneInstance instance;
            try
            {
                instance = await handle.ToUniTask(progress, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
                throw;
            }

            var sceneName = instance.Scene.name;
            var assetScope = _assetService.CreateScope($"scene:{sceneName}");

            // The child container is what lets scene-scoped services be registered
            // per scene while still resolving root singletons.
            var resolver = _rootResolver.CreateScope(builder =>
            {
                builder.RegisterInstance(assetScope).As<IAssetScope>();
            });

            return new SceneScope(sceneName, handle, resolver, assetScope);
        }

        public void Dispose()
        {
            for (var i = _additiveScopes.Count - 1; i >= 0; i--)
                _additiveScopes[i].Dispose();
            _additiveScopes.Clear();

            _activeScope?.Dispose();
            _activeScope = null;
        }
    }
}
