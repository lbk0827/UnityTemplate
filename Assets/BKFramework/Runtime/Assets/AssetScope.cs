using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using BK.Core.Diagnostics;
using Object = UnityEngine.Object;

namespace BK.Assets
{
    /// <summary>
    /// Addressables-backed <see cref="IAssetScope"/>. Every handle acquired here is
    /// recorded, so <see cref="Dispose"/> can release the whole set without callers
    /// tracking anything.
    /// </summary>
    internal sealed class AssetScope : IAssetScope
    {
        private readonly List<AsyncOperationHandle> _assetHandles = new();
        private readonly Dictionary<GameObject, AsyncOperationHandle<GameObject>> _instances = new();

        public AssetScope(string name) => Name = name;

        public string Name { get; }
        public bool IsDisposed { get; private set; }

        public async UniTask<T> LoadAsync<T>(AssetKey key, CancellationToken cancellationToken = default)
            where T : Object
        {
            ThrowIfDisposed();
            if (!key.IsValid)
                throw new ArgumentException("Asset key is empty.", nameof(key));

            var handle = Addressables.LoadAssetAsync<T>(key.Address);
            try
            {
                var asset = await handle.ToUniTask(cancellationToken: cancellationToken);

                // Disposal can race a load that was already in flight; if the scope
                // closed while we awaited, the handle has no owner left to release it.
                if (IsDisposed)
                {
                    Addressables.Release(handle);
                    throw new ObjectDisposedException(nameof(AssetScope), $"Scope '{Name}' closed while loading '{key}'.");
                }

                _assetHandles.Add(handle);
                return asset;
            }
            catch (OperationCanceledException)
            {
                Addressables.Release(handle);
                throw;
            }
        }

        public async UniTask<GameObject> InstantiateAsync(
            AssetKey key,
            Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!key.IsValid)
                throw new ArgumentException("Asset key is empty.", nameof(key));

            var handle = Addressables.InstantiateAsync(key.Address, parent);
            try
            {
                var instance = await handle.ToUniTask(cancellationToken: cancellationToken);

                if (IsDisposed)
                {
                    Addressables.ReleaseInstance(handle);
                    throw new ObjectDisposedException(nameof(AssetScope), $"Scope '{Name}' closed while instantiating '{key}'.");
                }

                _instances.Add(instance, handle);
                return instance;
            }
            catch (OperationCanceledException)
            {
                Addressables.ReleaseInstance(handle);
                throw;
            }
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (instance == null)
                return;

            if (!_instances.Remove(instance, out var handle))
            {
                BKLog.Warn(BKLog.Assets,
                    $"scope '{Name}' asked to release an instance it does not own: {instance.name}");
                return;
            }

            Addressables.ReleaseInstance(handle);
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;
            IsDisposed = true;

            foreach (var pair in _instances)
                ReleaseInstanceHandle(pair.Value);
            _instances.Clear();

            foreach (var handle in _assetHandles)
                ReleaseAssetHandle(handle);
            _assetHandles.Clear();

            BKLog.Verbose(BKLog.Assets, $"scope '{Name}' disposed");
        }

        private void ReleaseAssetHandle(AsyncOperationHandle handle)
        {
            try
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            catch (Exception ex)
            {
                BKLog.Warn(BKLog.Assets, $"scope '{Name}' skipped an invalid asset handle during dispose: {ex.Message}");
            }
        }

        private void ReleaseInstanceHandle(AsyncOperationHandle<GameObject> handle)
        {
            try
            {
                if (handle.IsValid())
                    Addressables.ReleaseInstance(handle);
            }
            catch (Exception ex)
            {
                BKLog.Warn(BKLog.Assets, $"scope '{Name}' skipped an invalid instance handle during dispose: {ex.Message}");
            }
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(AssetScope), $"Asset scope '{Name}' is disposed.");
        }
    }
}
