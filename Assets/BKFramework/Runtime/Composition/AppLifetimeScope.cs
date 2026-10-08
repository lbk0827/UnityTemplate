using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BK.Assets;
using BK.Core.App;
using BK.Core.Events;
using BK.Core.Time;
using BK.Data;
using BK.Localization;
using BK.Notifications;
using BK.Options;
using BK.Save;
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

            builder.Register<SystemClock>(Lifetime.Singleton).As<IClock>();
            builder.Register<EventBus>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<AddressablesAssetService>(Lifetime.Singleton).AsImplementedInterfaces().AsSelf();
            builder.Register<SceneService>(Lifetime.Singleton).AsImplementedInterfaces();

            var saveDirectory = SaveDirectory;
            builder.Register<ISaveService>(_ => new SaveService(saveDirectory), Lifetime.Singleton);
            builder.Register<IOptionsService>(container => new OptionsService(
                container.Resolve<ISaveService>(), _settings.DefaultLanguage), Lifetime.Singleton);
            NotificationsInstaller.Install(builder);

            builder.Register<ITableService>(container => new TableService(
                container.Resolve<IAssetService>(),
                new AssetKey(_settings.TableCatalogAddress)), Lifetime.Singleton);

            builder.Register<ILocalizationService>(container => new LocalizationService(
                container.Resolve<IAssetService>(),
                code => new AssetKey(_settings.LocalizationAddressFor(code))), Lifetime.Singleton);

            builder.Register(_ => UIRoot.Create(_settings.ReferenceResolution), Lifetime.Singleton);
            builder.Register<PopupDim>(Lifetime.Singleton).As<IPopupDim>();

            builder.Register<UIService>(Lifetime.Singleton).As<IUIService>();
            builder.Register<IScreenCover>(container => new ScreenCover(ScreenCoverView.Create(container.Resolve<UIRoot>())), Lifetime.Singleton);
            builder.Register<ISceneFlow>(container => new SceneFlow(
                container.Resolve<ISceneService>(),
                container.Resolve<IUIService>(),
                container.Resolve<IScreenCover>(),
                container.Resolve<IPopupDim>()), Lifetime.Singleton);
            builder.Register<IMessagePresenter>(container => new UIMessagePresenter(
                container.Resolve<IUIService>(),
                new AssetKey(_settings.MessagePopupAddress),
                new AssetKey(_settings.ToastAddress),
                _settings.ToastSeconds), Lifetime.Singleton);
            builder.Register<MessageService>(Lifetime.Singleton).As<IMessageService>();

            RegisterBootSteps(builder);

            builder.Register<BootSequence>(Lifetime.Singleton);
            builder.Register<IProgress<BootProgress>, NullBootProgress>(Lifetime.Singleton);
            builder.RegisterEntryPoint<AppBootstrapper>();
            builder.RegisterEntryPoint<BackInputDriver>().AsSelf();

            ConfigureProject(builder);
        }

        /// <summary>
        /// Project-side registrations (entry points, game services, extra boot steps).
        /// Subclass in the game assembly and override this instead of editing the framework.
        /// </summary>
        protected virtual void ConfigureProject(IContainerBuilder builder) { }

        /// <summary>
        /// Folder for save slots. Override to isolate test runs or to namespace a game's profile.
        /// </summary>
        protected virtual string SaveDirectory => SaveService.DefaultDirectory;

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
                container.Resolve<IOptionsService>().Language.Value), Lifetime.Singleton);
        }
    }
}
