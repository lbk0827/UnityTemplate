using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;
using BK.Core.App;
using BK.Core.Diagnostics;
using BK.Core.Events;
using BK.Scene;
using BK.UI;

namespace Project
{
    /// <summary>
    /// 게임 흐름의 시작점. 부트가 끝나면 Lobby 씬으로 전환하고 Lobby 뷰를 엽니다.
    /// 프로젝트별 흐름은 여기서 시작해 확장하면 됩니다.
    /// </summary>
    public sealed class GameFlow : IStartable, IDisposable
    {
        public const string LobbySceneAddress = "Scenes/Lobby";
        public const string LobbyViewAddress = "UI/LobbyView";

        private readonly IEventBus _events;
        private readonly ISceneService _scenes;
        private readonly IUIService _ui;
        private readonly CancellationTokenSource _cts = new();
        private IDisposable _subscription;

        public GameFlow(IEventBus events, ISceneService scenes, IUIService ui)
        {
            _events = events;
            _scenes = scenes;
            _ui = ui;
        }

        public void Start()
        {
            _subscription = _events.Receive<AppBootCompleted>()
                .Subscribe(_ => EnterLobbyAsync(_cts.Token).Forget());
        }

        private async UniTask EnterLobbyAsync(CancellationToken cancellationToken)
        {
            await _scenes.TransitionToAsync(LobbySceneAddress, cancellationToken: cancellationToken);
            await _ui.OpenAsync<LobbyView>(LobbyViewAddress, cancellationToken);
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
