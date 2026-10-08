using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using BK.Assets;
using BK.Scene;

namespace BK.UI
{
    /// <inheritdoc cref="ISceneFlow"/>
    public sealed class SceneFlow : ISceneFlow
    {
        private readonly ISceneService _scenes;
        private readonly IUIService _ui;
        private readonly IScreenCover _cover;
        private readonly IPopupDim _dim;
        private readonly float _fadeInSeconds;

        public SceneFlow(ISceneService scenes, IUIService ui, IScreenCover cover, IPopupDim dim, float fadeInSeconds = 0.15f)
        {
            _scenes = scenes;
            _ui = ui;
            _cover = cover;
            _dim = dim;
            _fadeInSeconds = fadeInSeconds;
        }

        public bool IsBusy { get; private set; }

        public async UniTask<ISceneScope> TransitionAsync(AssetKey scene, CancellationToken cancellationToken = default)
        {
            if (IsBusy)
                throw new InvalidOperationException("A scene flow is already running.");
            IsBusy = true;
            try
            {
                await _cover.HoldAsync(_fadeInSeconds, cancellationToken);
                _dim.Reset();
                await _ui.CloseAllAsync(UILayer.Popup, cancellationToken);
                await _ui.CloseAllAsync(UILayer.Content, cancellationToken);
                var scope = await _scenes.TransitionToAsync(scene, null, cancellationToken);
                await _cover.ReleaseWhenReadyAsync(cancellationToken);
                return scope;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
