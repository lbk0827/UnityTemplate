using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BK.Assets;
using BK.Core.App;
using BK.Core.Events;
using BK.Data;
using BK.Localization;
using BK.Scene;
using BK.UI;

namespace BK.Composition
{
    /// <summary>
    /// The composition root. Put one in the bootstrap scene; it survives scene loads
    /// and every other scope descends from it.
    ///
    /// Nothing registered here may touch Addressables in its constructor: entry points
    /// are built with the container, before the asset boot step runs. Services that need
    /// the global asset scope resolve it lazily on first use.
    /// </summary>
    public class AppLifetimeScope : LifetimeScope
    {
        [SerializeField] private FrameworkSettings _settings;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_settings == null)
                throw new InvalidOperationException(
                    $"{nameof(AppLifetimeScope)} has no {nameof(FrameworkSettings)} assigned.");

            builder.RegisterInstance(_settings);

            builder.Register<EventBus>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<AddressablesAssetService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<SceneService>(Lifetime.Singleton).AsImplementedInterfaces();

            builder.Register<ITableService>(container => new TableService(
                container.Resolve<IAssetService>(),
                new AssetKey(_settings.TableCatalogAddress)), Lifetime.Singleton);

            builder.Register<ILocalizationService>(container => new LocalizationService(
                container.Resolve<IAssetService>(),
                code => new AssetKey(_settings.LocalizationAddressFor(code))), Lifetime.Singleton);

            builder.Register(_ => UIRoot.Create(_settings.ReferenceResolution), Lifetime.Singleton);

            builder.Register<UIService>(Lifetime.Singleton).As<IUIService>();

            RegisterBootSteps(builder);

            builder.Register<BootSequence>(Lifetime.Singleton);
            builder.Register<IProgress<BootProgress>, NullBootProgress>(Lifetime.Singleton);
            builder.RegisterEntryPoint<AppBootstrapper>();

            ConfigureProject(builder);
        }

        /// <summary>
        /// Project-side registrations (entry points, game services, extra boot steps).
        /// Subclass in the game assembly and override this instead of editing the framework.
        /// </summary>
        protected virtual void ConfigureProject(IContainerBuilder builder) { }

        /// <summary>
        /// Order comes from each step's <see cref="IBootStep.Order"/>, not from this
        /// list, so registration order here is irrelevant.
        /// </summary>
        private static void RegisterBootSteps(IContainerBuilder builder)
        {
            builder.Register<AssetBootStep>(Lifetime.Singleton).As<IBootStep>();
            builder.Register<DataBootStep>(Lifetime.Singleton).As<IBootStep>();
            builder.Register<IBootStep>(container => new LocalizationBootStep(
                container.Resolve<ILocalizationService>(),
                container.Resolve<FrameworkSettings>().DefaultLanguage), Lifetime.Singleton);
        }
    }
}
