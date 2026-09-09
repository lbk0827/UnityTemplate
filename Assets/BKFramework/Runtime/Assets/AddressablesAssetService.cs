using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using BK.Core.Diagnostics;

namespace BK.Assets
{
    /// <summary>
    /// Default <see cref="IAssetService"/>. Owns the Addressables bootstrap and hands
    /// out scopes; it deliberately exposes no direct load API so that every load has
    /// an owner that can release it.
    /// </summary>
    public sealed class AddressablesAssetService : IAssetService, IDisposable
    {
        private readonly List<AssetScope> _scopes = new();
        private AssetScope _globalScope;
        private bool _initialized;

        public IAssetScope GlobalScope
            => _globalScope ?? throw new InvalidOperationException(
                $"{nameof(AddressablesAssetService)} is not initialised. Run the asset boot step first.");

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
                return;

            await Addressables.InitializeAsync().ToUniTask(cancellationToken: cancellationToken);

            _globalScope = new AssetScope("global");
            _initialized = true;
            BKLog.Info(BKLog.Assets, "addressables initialised");
        }

        public IAssetScope CreateScope(string name)
        {
            if (!_initialized)
                throw new InvalidOperationException(
                    $"{nameof(AddressablesAssetService)} is not initialised. Run the asset boot step first.");

            var scope = new AssetScope(name);
            _scopes.Add(scope);
            return scope;
        }

        /// <summary>
        /// Disposes every scope this service created, global last. Called on app
        /// shutdown; individual scopes normally dispose themselves earlier.
        /// </summary>
        public void Dispose()
        {
            for (var i = _scopes.Count - 1; i >= 0; i--)
                _scopes[i].Dispose();
            _scopes.Clear();

            _globalScope?.Dispose();
            _globalScope = null;
            _initialized = false;
        }
    }
}
