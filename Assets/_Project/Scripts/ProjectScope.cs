using VContainer;
using VContainer.Unity;
using BK.Composition;

namespace Project
{
    /// <summary>부트스트랩 씬에 배치하는 조립 루트. 프로젝트 고유 등록은 전부 여기서.</summary>
    public sealed class ProjectScope : AppLifetimeScope
    {
        protected override void ConfigureProject(IContainerBuilder builder)
        {
            // 플레이어 상태. 세션 전체를 살고, 시간 충전이 필요한 것은 Tick 을 받습니다.
            builder.RegisterEntryPoint<PlayerWallet>().AsSelf();
            builder.RegisterEntryPoint<DailyRewardState>().AsSelf();
            builder.Register<PlayerProfile>(Lifetime.Singleton);
            builder.Register<GameOptions>(c => new GameOptions(c.Resolve<FrameworkSettings>().DefaultLanguage), Lifetime.Singleton);

            builder.Register<LobbyState>(Lifetime.Singleton);
            builder.Register<ShopService>(Lifetime.Singleton);
            builder.Register<MessageService>(Lifetime.Singleton);

            builder.RegisterEntryPoint<GameFlow>();
        }
    }
}
