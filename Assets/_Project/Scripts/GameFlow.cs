using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;
using BK.Core.App;
using BK.Core.Diagnostics;
using BK.Core.Events;
using BK.Localization;
using BK.Scene;
using BK.UI;

namespace Project
{
    /// <summary>
    /// 게임 흐름의 시작점. 부트가 끝나면 저장된 언어를 적용하고 Lobby 씬으로 전환한 뒤
    /// 로비 패널과 HUD 를 엽니다. 프로젝트별 흐름은 여기서 시작해 확장하면 됩니다.
    /// </summary>
    public sealed class GameFlow : IStartable, IDisposable
    {
        public const string LobbySceneAddress = "Scenes/Lobby";

        private readonly IEventBus _events;
        private readonly ISceneService _scenes;
        private readonly IUIService _ui;
        private readonly ILocalizationService _loc;
        private readonly GameOptions _options;
        private readonly CancellationTokenSource _cts = new();
        private IDisposable _subscription;

        public GameFlow(IEventBus events, ISceneService scenes, IUIService ui, ILocalizationService loc, GameOptions options)
        {
            _events = events;
            _scenes = scenes;
            _ui = ui;
            _loc = loc;
            _options = options;
        }

        public void Start()
        {
            _subscription = _events.Receive<AppBootCompleted>()
                .Subscribe(_ => EnterLobbyAsync(_cts.Token).Forget());
        }

        private async UniTask EnterLobbyAsync(CancellationToken cancellationToken)
        {
            if (_options.Language.Value != _loc.CurrentLanguage.CurrentValue)
                await _loc.SetLanguageAsync(_options.Language.Value, cancellationToken);

            await _scenes.TransitionToAsync(LobbySceneAddress, cancellationToken: cancellationToken);
            await _ui.OpenAsync<LobbyPanelView>(LobbyPanelView.Address, cancellationToken);
            await _ui.OpenAsync<HudView>(HudView.Address, cancellationToken);
            BKLog.Info("Flow", "lobby ready");
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _subscription?.Dispose();
        }
    }
}
