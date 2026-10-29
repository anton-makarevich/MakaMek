using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using MonacoLabs.Presto;
using Sanet.MakaMek.Avalonia.Behaviors;
using Sanet.MakaMek.Avalonia.Controls.Extensions;
using Sanet.MakaMek.Avalonia.DI;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Avalonia.Views.About;
using Sanet.MakaMek.Avalonia.Views.EndGame;
using Sanet.MakaMek.Avalonia.Views.JoinGame;
using Sanet.MakaMek.Avalonia.Views.Settings;
using Sanet.MakaMek.Avalonia.Views.StartNewGame;
using Sanet.MakaMek.Avalonia.Views.MainMenu;
using Sanet.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.Services;
using Sanet.MVVM.Navigation.Avalonia.Services;
using MainWindow = Sanet.MakaMek.Avalonia.Views.MainWindow;

namespace Sanet.MakaMek.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
    
    public IServiceProvider? ServiceProvider { get; private set; }

    public override void OnFrameworkInitializationCompleted()
    {
        // Presto is the motion layer of the client. Follow the OS reduce-motion setting, so every
        // animation with a reduced form degrades to a short fade instead of moving.
        PrestoAvalonia.EnsureInitialized();
        PrestoAvalonia.MotionPreference = MotionPreference.System;

        if (Resources[MVVM.DI.Avalonia.Extensions.AppBuilderExtensions.ServiceCollectionResourceKey] is not IServiceCollection services)
        {
            throw new Exception("Services are not initialized");
        }

        services.RegisterServices();
        services.RegisterViewModels();

        var serviceProvider = services.BuildServiceProvider();
        ServiceProvider = serviceProvider;

        // Resolving eagerly runs the image service factory, so a missing service or logger fails
        // here rather than silently leaving the unit cards without an image. The controls only
        // hold the platform-specific cast; a non-bitmap registration is a wiring error.
        var imageService = serviceProvider.GetRequiredService<IImageService>();
        if (imageService is not IImageService<Bitmap>)
        {
            throw new InvalidOperationException(
                $"{nameof(IImageService)} must be registered with a bitmap implementation, but {imageService.GetType().FullName} was registered.");
        }

        // Resolve converters and expose them (and the localization service) as application
        // resources so {StaticResource ...} and the Localize markup extension can use them.
        var localizationService = serviceProvider.GetRequiredService<ILocalizationService>();
        Resources[LocalizeExtension.LocalizationServiceResourceKey] = localizationService;
        Resources[nameof(Converters.ModifierToTextConverter)] = serviceProvider.GetRequiredService<Converters.ModifierToTextConverter>();
        Resources[nameof(Converters.SegmentEventToTextConverter)] = serviceProvider.GetRequiredService<Converters.SegmentEventToTextConverter>();
        Resources[nameof(Converters.ConsciousnessStatusConverter)] = serviceProvider.GetRequiredService<Converters.ConsciousnessStatusConverter>();
        Resources[nameof(Converters.MovementBreakdownConverter)] = serviceProvider.GetRequiredService<Converters.MovementBreakdownConverter>();
        Resources[nameof(Converters.HubStatusTextConverter)] = serviceProvider.GetRequiredService<Converters.HubStatusTextConverter>();
        Resources[nameof(Converters.ConnectionStatusTextConverter)] = serviceProvider.GetRequiredService<Converters.ConnectionStatusTextConverter>();
        Resources[nameof(Converters.ComponentStatusBackgroundConverter)] = serviceProvider.GetRequiredService<Converters.ComponentStatusBackgroundConverter>();
        Resources[nameof(Converters.EventTypeToBackgroundConverter)] = serviceProvider.GetRequiredService<Converters.EventTypeToBackgroundConverter>();
        Resources[nameof(Converters.ConsciousnessColorConverter)] = serviceProvider.GetRequiredService<Converters.ConsciousnessColorConverter>();
        Resources[nameof(Converters.SelectedItemToBrushConverter)] = serviceProvider.GetRequiredService<Converters.SelectedItemToBrushConverter>();
        Resources[nameof(Converters.HubStatusBackgroundConverter)] = serviceProvider.GetRequiredService<Converters.HubStatusBackgroundConverter>();
        Resources[nameof(Converters.ConnectionStatusBackgroundConverter)] = serviceProvider.GetRequiredService<Converters.ConnectionStatusBackgroundConverter>();

        INavigationService navigationService;

        MainMenuViewModel? viewModel;
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                navigationService = new NavigationService(desktop, serviceProvider);
                RegisterViews(navigationService);
                viewModel = navigationService.GetViewModel<MainMenuViewModel>();
                var mainMenuContent = new MainMenuView
                {
                    ViewModel = viewModel
                };
                KeyboardAwareBehavior.SetIsEnabled(mainMenuContent, true);
                desktop.MainWindow = new MainWindow
                {
                    Content = mainMenuContent
                };
                break;
            case IActivityApplicationLifetime activityLifetime:
                var androidSingleViewLifeTime = activityLifetime as ISingleViewApplicationLifetime;
                if (androidSingleViewLifeTime == null)
                {
                    throw new Exception("Android SingleViewApplicationLifetime is not available");
                }
                var androidViewWrapper = new ContentControl();
                KeyboardAwareBehavior.SetIsEnabled(androidViewWrapper, true);
                navigationService =
                    new SingleViewNavigationService(
                        androidSingleViewLifeTime,
                        androidViewWrapper, serviceProvider);
                RegisterViews(navigationService);
                viewModel = navigationService.GetViewModel<MainMenuViewModel>();
                var androidMenuView =new MainMenuView()
                                                     {
                                                         ViewModel = viewModel
                                                     };
                androidViewWrapper.Content = androidMenuView;
                activityLifetime.MainViewFactory = () => androidViewWrapper;
                break;
            case ISingleViewApplicationLifetime singleViewPlatform:
                var mainViewWrapper = new ContentControl();
                KeyboardAwareBehavior.SetIsEnabled(mainViewWrapper, true);
                navigationService =
                    new SingleViewNavigationService(singleViewPlatform, mainViewWrapper, serviceProvider);
                RegisterViews(navigationService);
                viewModel = navigationService.GetViewModel<MainMenuViewModel>();
                mainViewWrapper.Content = new MainMenuView()
                {
                    ViewModel = viewModel
                };
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void RegisterViews(INavigationService navigationService)
    {
        // Register Main Menu view (using the single view for all platforms)
        navigationService.RegisterViews(typeof(MainMenuView), typeof(MainMenuViewModel));

        if (IsMobile())
        {
            navigationService.RegisterViews(typeof(StartNewGameViewNarrow), typeof(StartNewGameViewModel));
            navigationService.RegisterViews(typeof(JoinGameViewNarrow), typeof(JoinGameViewModel));
            navigationService.RegisterViews(typeof(SettingsViewNarrow), typeof(SettingsViewModel));
        }
        else
        {
            navigationService.RegisterViews(typeof(StartNewGameViewWide), typeof(StartNewGameViewModel));
            navigationService.RegisterViews(typeof(JoinGameViewWide), typeof(JoinGameViewModel));
            navigationService.RegisterViews(typeof(SettingsViewWide), typeof(SettingsViewModel));
        }

        // Register views that are the same for all platforms
        navigationService.RegisterViews(typeof(BattleMapView), typeof(BattleMapViewModel));
        navigationService.RegisterViews(typeof(EndGameView), typeof(EndGameViewModel));
        navigationService.RegisterViews(typeof(AboutView), typeof(AboutViewModel));
        navigationService.RegisterViews(typeof(AvailableUnitsTableView), typeof(AvailableUnitsTableViewModel));
        navigationService.RegisterViews(typeof(UnitInfoView), typeof(UnitInfoViewModel));
        navigationService.RegisterViews(typeof(AddHubView), typeof(AddHubViewModel));
        navigationService.RegisterViews(typeof(AddProviderView), typeof(AddProviderViewModel));
    }
    private bool IsMobile()
    {
        return OperatingSystem.IsIOS() || OperatingSystem.IsAndroid();
    }
}