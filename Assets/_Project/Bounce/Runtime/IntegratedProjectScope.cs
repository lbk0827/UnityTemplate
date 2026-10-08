using System;
using System.IO;
using BK.Composition;
using BK.Core.App;
using BK.Core.Events;
using BK.Core.Time;
using BK.Meta;
using BK.Notifications;
using BK.Options;
using BK.Save;
using BK.Scene;
using BK.UI;
using BK.Data;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BK.Kit
{
    // The original sample ProjectScope remains available, but is not instantiated in this boot scene.
    public sealed class IntegratedProjectScope : AppLifetimeScope
    {
        /// <summary>Tests point this at an isolated folder so the real profile is never touched.</summary>
        public const string TestSaveDirectoryVariable = "BK_KIT_TEST_SAVE_DIR";

        protected override string SaveDirectory
        {
            get
            {
                var folder = Environment.GetEnvironmentVariable(TestSaveDirectoryVariable);
                return string.IsNullOrEmpty(folder)
                    ? Path.Combine(Application.persistentDataPath, "Bounce", "Profiles", "local")
                    : folder;
            }
        }

        protected override void ConfigureProject(IContainerBuilder builder)
        {
            MetaInstaller.Install(builder, new BounceCurrencies(), new BounceContinueOffers(), BounceEntry.Policy);
            builder.Register<KitServices>(Lifetime.Singleton);
            builder.Register<IPushProvider>(container => new HeartFullPushProvider(container.Resolve<IWallet>(), BounceCurrencies.Heart, 1001,
                "Hearts are full", "Your hearts are back. Ready for the next stage?"), Lifetime.Singleton);
            builder.Register<IPushProvider>(_ => new DailyRetentionPushProvider(new DailyRetentionConfig
            {
                Title = "Bounce",
                Bodies = new[]
                {
                    "The blocks are waiting. One quick round?",
                    "Your cannon misses you. Come back for a stage!",
                    "New day, new high score. Play a round now.",
                    "Hearts are full. Time to bounce!",
                    "A stage a day keeps the blocks away.",
                },
            }), Lifetime.Singleton);
            builder.RegisterEntryPoint<IntegratedGameFlow>();
        }
    }

    /// <summary>Everything KitApp needs from the framework, resolved once from the app scope.</summary>
    public sealed class KitServices
    {
        public readonly ISceneService Scenes;
        public readonly IUIService UI;
        public readonly ITableService Tables;
        public readonly ISaveService Saves;
        public readonly IWallet Wallet;
        public readonly IStageProgress Progress;
        public readonly IOptionsService Options;
        public readonly IMessageService Messages;
        public readonly PendingRewardQueue Rewards;
        public readonly CurrencyDisplayLock DisplayLock;
        public readonly ContinueOffers Continues;
        public readonly IClock Clock;
        public readonly LocalPushService Push;

        public KitServices(ISceneService scenes, IUIService ui, ITableService tables, ISaveService saves, IWallet wallet,
            IStageProgress progress, IOptionsService options, IMessageService messages, PendingRewardQueue rewards,
            CurrencyDisplayLock displayLock, ContinueOffers continues, IClock clock, LocalPushService push)
        {
            Scenes = scenes; UI = ui; Tables = tables; Saves = saves; Wallet = wallet; Progress = progress;
            Options = options; Messages = messages; Rewards = rewards; DisplayLock = displayLock; Continues = continues; Clock = clock;
            Push = push;
        }
    }

    public sealed class IntegratedGameFlow : IStartable, IDisposable
    {
        private readonly IEventBus events;
        private readonly KitServices services;
        private IDisposable subscription;
        private KitApp app;

        public IntegratedGameFlow(IEventBus events, KitServices services)
        {
            this.events = events;
            this.services = services;
        }

        public void Start()
        {
            subscription = events.Receive<AppBootCompleted>().Subscribe(_ =>
            {
                if (app != null) return;
                app = new GameObject("BounceSession").AddComponent<KitApp>();
                app.Initialize(services);
            });
        }

        public void Dispose()
        {
            subscription?.Dispose();
            if (app != null) UnityEngine.Object.Destroy(app.gameObject);
        }
    }
}
