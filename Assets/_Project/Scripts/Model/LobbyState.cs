using System;
using R3;

namespace Project
{
    public enum LobbyTab
    {
        Store = 0,
        Home = 1,
    }

    /// <summary>로비 탭 상태. HUD 탭 버튼과 로비 패널이 이 값 하나를 공유합니다.</summary>
    public sealed class LobbyState : IDisposable
    {
        public ReactiveProperty<LobbyTab> CurrentTab { get; } = new(LobbyTab.Home);

        public void Dispose() => CurrentTab.Dispose();
    }
}
