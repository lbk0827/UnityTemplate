using System;
using BK.Composition;
using BK.Core.App;
using BK.Core.Events;
using BK.Scene;
using BK.UI;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BK.Kit
{
    // The original sample ProjectScope remains available, but is not instantiated in this boot scene.
    public sealed class IntegratedProjectScope : AppLifetimeScope
    {
        protected override void ConfigureProject(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<IntegratedGameFlow>();
        }
    }

    public sealed class IntegratedGameFlow : IStartable, IDisposable
    {
        private readonly IEventBus events;
        private readonly ISceneService scenes;
        private readonly IUIService ui;
        private IDisposable subscription;
        private KitApp app;

        public IntegratedGameFlow(IEventBus events, ISceneService scenes, IUIService ui)
        { this.events = events; this.scenes = scenes; this.ui = ui; }

        public void Start()
        {
            subscription = events.Receive<AppBootCompleted>().Subscribe(_ =>
            {
                if (app != null) return;
                app = new GameObject("BounceSession").AddComponent<KitApp>();
                app.Initialize(scenes, ui);
            });
        }

        public void Dispose()
        {
            subscription?.Dispose();
            if (app != null) UnityEngine.Object.Destroy(app.gameObject);
        }
    }
}
