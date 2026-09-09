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
            builder.RegisterEntryPoint<GameFlow>();
        }
    }
}
