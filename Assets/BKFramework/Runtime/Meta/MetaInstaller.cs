using VContainer;
using VContainer.Unity;

namespace BK.Meta
{
    /// <summary>
    /// Registers the meta loop in a game's scope. Not part of the default app scope
    /// because it needs the game's currency and continue catalogs.
    /// Call from <c>AppLifetimeScope.ConfigureProject</c>.
    /// </summary>
    public static class MetaInstaller
    {
        public static void Install(IContainerBuilder builder, ICurrencyCatalog currencies, IContinueOfferCatalog continues, StageEntryPolicy entryPolicy,
            IStepOfferCatalog stepOffers = null, IDailyRewardCatalog dailyRewards = null, IWinStreakCatalog winStreak = null)
        {
            builder.RegisterInstance(currencies);
            builder.RegisterInstance(continues);
            builder.RegisterInstance(entryPolicy);

            builder.Register<Wallet>(Lifetime.Singleton).As<IWallet>();
            builder.RegisterEntryPoint<WalletTicker>();
            builder.Register<StageProgress>(Lifetime.Singleton).As<IStageProgress>();
            builder.Register<PendingRewardQueue>(Lifetime.Singleton);
            builder.Register<CurrencyDisplayLock>(Lifetime.Singleton);
            builder.Register<ContinueOffers>(Lifetime.Singleton);
            builder.Register<StagePreloaderRegistry>(Lifetime.Singleton).As<IStagePreloaderRegistry>();

            // Optional content: registered only when the game supplies its catalog.
            if (stepOffers != null)
            {
                builder.RegisterInstance(stepOffers);
                builder.Register<StepOffers>(Lifetime.Singleton);
            }
            if (dailyRewards != null)
            {
                builder.RegisterInstance(dailyRewards);
                builder.Register<DailyRewards>(Lifetime.Singleton);
            }
            if (winStreak != null)
            {
                builder.RegisterInstance(winStreak);
                builder.Register<WinStreak>(Lifetime.Singleton);
            }
        }
    }
}
