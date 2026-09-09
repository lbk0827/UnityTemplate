using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BK.Assets;
using BK.Core.Diagnostics;

namespace BK.UI
{
    /// <inheritdoc cref="IUIService"/>
    public sealed class UIService : IUIService, IDisposable
    {
        private readonly Dictionary<UILayer, List<IUIView>> _stacks = new();
        private readonly Dictionary<IUIView, GameObject> _instances = new();
        private readonly IObjectResolver _resolver;
        private readonly IAssetScope _assets;
        private readonly UIRoot _root;

        /// <param name="assets">
        /// Scope that owns view prefabs. Pass a scene's scope for scene-local views so
        /// they are released with the scene; pass the global scope for persistent UI.
        /// </param>
        public UIService(IObjectResolver resolver, IAssetScope assets, UIRoot root)
        {
            _resolver = resolver;
            _assets = assets;
            _root = root;

            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
                _stacks.Add(layer, new List<IUIView>());
        }

        public UniTask<TView> OpenAsync<TView>(AssetKey key, CancellationToken cancellationToken = default)
            where TView : class, IUIView
            => OpenInternalAsync<TView>(key, null, cancellationToken);

        public UniTask<TView> OpenAsync<TView, TArgs>(
            AssetKey key,
            TArgs args,
            CancellationToken cancellationToken = default)
            where TView : class, IUIView<TArgs>
            => OpenInternalAsync<TView>(key, view => ((IUIView<TArgs>)view).SetArgs(args), cancellationToken);

        private async UniTask<TView> OpenInternalAsync<TView>(
            AssetKey key,
            Action<IUIView> applyArgs,
            CancellationToken cancellationToken)
            where TView : class, IUIView
        {
            var instance = await _assets.InstantiateAsync(key, null, cancellationToken);

            var view = instance.GetComponent<TView>();
            if (view == null)
            {
                _assets.ReleaseInstance(instance);
                throw new InvalidOperationException(
                    $"Prefab '{key}' has no {typeof(TView).Name} component.");
            }

            // Parent after the component check so a bad prefab never lands on a canvas.
            instance.transform.SetParent(_root.GetLayerRoot(view.Layer), false);
            _resolver.InjectGameObject(instance);

            applyArgs?.Invoke(view);

            await view.OnInitializeAsync(cancellationToken);

            _instances.Add(view, instance);
            _stacks[view.Layer].Add(view);

            await view.OnOpenAsync(cancellationToken);
            BKLog.Verbose(BKLog.UI, $"opened {typeof(TView).Name} on {view.Layer}");
            return view;
        }

        public async UniTask CloseAsync(IUIView view, CancellationToken cancellationToken = default)
        {
            if (view == null || !_instances.TryGetValue(view, out var instance))
                return;

            // Unstack before the transition so a second close cannot start on the
            // same view while the first is still animating.
            _stacks[view.Layer].Remove(view);
            _instances.Remove(view);

            await view.OnCloseAsync(cancellationToken);
            _assets.ReleaseInstance(instance);
        }

        public UniTask CloseTopAsync(UILayer layer, CancellationToken cancellationToken = default)
        {
            var top = Peek(layer);
            return top == null ? UniTask.CompletedTask : CloseAsync(top, cancellationToken);
        }

        public async UniTask CloseAllAsync(UILayer layer, CancellationToken cancellationToken = default)
        {
            var stack = _stacks[layer];
            while (stack.Count > 0)
                await CloseAsync(stack[^1], cancellationToken);
        }

        public IUIView Peek(UILayer layer)
        {
            var stack = _stacks[layer];
            return stack.Count > 0 ? stack[^1] : null;
        }

        public bool HandleBackRequest()
        {
            // Highest layer first: a system dialog outranks a popup, which outranks HUD.
            var layers = (UILayer[])Enum.GetValues(typeof(UILayer));
            for (var i = layers.Length - 1; i >= 0; i--)
            {
                var top = Peek(layers[i]);
                if (top == null)
                    continue;

                if (top.OnBackRequested())
                    return true;

                CloseAsync(top).Forget();
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            foreach (var pair in _instances)
                _assets.ReleaseInstance(pair.Value);

            _instances.Clear();
            foreach (var stack in _stacks.Values)
                stack.Clear();
        }
    }
}
