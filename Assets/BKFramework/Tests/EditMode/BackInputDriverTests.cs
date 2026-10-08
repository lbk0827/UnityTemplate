using System.Threading;
using BK.Assets;
using BK.Scene;
using BK.UI;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace BK.Tests
{
    public sealed class BackInputDriverTests
    {
        private sealed class FakeUI : IUIService
        {
            public int BackRequests;
            public bool HandleBackRequest() { BackRequests++; return true; }
            public UniTask<TView> OpenAsync<TView>(AssetKey key, CancellationToken ct = default) where TView : class, IUIView => throw new System.NotSupportedException();
            public UniTask<TView> OpenAsync<TView, TArgs>(AssetKey key, TArgs args, CancellationToken ct = default) where TView : class, IUIView<TArgs> => throw new System.NotSupportedException();
            public UniTask CloseAsync(IUIView view, CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask CloseTopAsync(UILayer layer, CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask CloseAllAsync(UILayer layer, CancellationToken ct = default) => UniTask.CompletedTask;
            public IUIView Peek(UILayer layer) => null;
        }

        private sealed class FakeScenes : ISceneService
        {
            public bool IsTransitioning { get; set; }
            public ISceneScope ActiveScene => null;
            public UniTask<ISceneScope> TransitionToAsync(AssetKey key, System.IProgress<float> progress = null, CancellationToken ct = default) => throw new System.NotSupportedException();
            public UniTask<ISceneScope> LoadAdditiveAsync(AssetKey key, System.IProgress<float> progress = null, CancellationToken ct = default) => throw new System.NotSupportedException();
            public UniTask UnloadAsync(ISceneScope scope, CancellationToken ct = default) => UniTask.CompletedTask;
        }

        [Test]
        public void PressRoutesToUIServiceOncePerPress()
        {
            var ui = new FakeUI(); var scenes = new FakeScenes();
            var pressed = false;
            var driver = new BackInputDriver(ui, scenes, () => pressed);
            driver.Tick();
            Assert.That(ui.BackRequests, Is.Zero);
            pressed = true; driver.Tick();
            Assert.That(ui.BackRequests, Is.EqualTo(1));
        }

        [Test]
        public void PressIsIgnoredWhileSceneTransitionsOrWhenSuspended()
        {
            var ui = new FakeUI(); var scenes = new FakeScenes { IsTransitioning = true };
            var driver = new BackInputDriver(ui, scenes, () => true);
            driver.Tick();
            Assert.That(ui.BackRequests, Is.Zero);
            scenes.IsTransitioning = false;
            driver.Suspended = true; driver.Tick();
            Assert.That(ui.BackRequests, Is.Zero);
            driver.Suspended = false; driver.Tick();
            Assert.That(ui.BackRequests, Is.EqualTo(1));
        }
    }
}
