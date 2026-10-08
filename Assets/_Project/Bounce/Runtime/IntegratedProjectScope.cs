using System;
using System.IO;
using BK.Composition;
using BK.Core.App;
using BK.Core.Events;
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

        private static bool IsTestRun => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(TestSaveDirectoryVariable));

        protected override string SaveDirectory
            => IsTestRun ? Environment.GetEnvironmentVariable(TestSaveDirectoryVariable)
                         : Path.Combine(Application.persistentDataPath, "Bounce", "Profiles", "local");

        // Tests flush explicitly and assert on files immediately; a background flusher would race their blocked-write windows.
        protected override float SaveFlushIntervalSeconds => IsTestRun ? 0f : base.SaveFlushIntervalSeconds;

        protected override void ConfigureProject(IContainerBuilder builder)
        {
            MetaInstaller.Install(builder, new BounceCurrencies(), new BounceContinueOffers(), BounceEntry.Policy,
                new BounceStepOffers(), new BounceDailyRewards(), new BounceWinStreak());
            builder.Register<KitServices>(Lifetime.Singleton);
            // Distinct implementation types: VContainer rejects two factory registrations under the same type in a collection.
            builder.Register(container => new HeartFullPushProvider(container.Resolve<IWallet>(), BounceCurrencies.Heart, 1001,
                "Hearts are full", "Your hearts are back. Ready for the next stage?"), Lifetime.Singleton).As<IPushProvider>();
            builder.Register(_ => new DailyRetentionPushProvider(new DailyRetentionConfig
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
            }), Lifetime.Singleton).As<IPushProvider>();
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
        public readonly LocalPushService Push;
        public readonly StepOffers Offers;
        public readonly DailyRewards Daily;
        public readonly WinStreak Streak;

        public KitServices(ISceneService scenes, IUIService ui, ITableService tables, ISaveService saves, IWallet wallet,
            IStageProgress progress, IOptionsService options, IMessageService messages, PendingRewardQueue rewards,
            CurrencyDisplayLock displayLock, ContinueOffers continues, LocalPushService push,
            StepOffers offers, DailyRewards daily, WinStreak streak)
        {
            Scenes = scenes; UI = ui; Tables = tables; Saves = saves; Wallet = wallet; Progress = progress;
            Options = options; Messages = messages; Rewards = rewards; DisplayLock = displayLock; Continues = continues;
            Push = push; Offers = offers; Daily = daily; Streak = streak;
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
