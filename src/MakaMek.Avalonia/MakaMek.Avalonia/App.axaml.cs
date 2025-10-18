using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia.DI;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Avalonia.Views;
using Sanet.MakaMek.Avalonia.Views.About;
using Sanet.MakaMek.Avalonia.Views.EndGame;
using Sanet.MakaMek.Avalonia.Views.JoinGame;
using Sanet.MakaMek.Avalonia.Views.StartNewGame;
using Sanet.MakaMek.Avalonia.Views.MainMenu;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
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
        if (Resources[MVVM.DI.Avalonia.Extensions.AppBuilderExtensions.ServiceCollectionResourceKey] is not IServiceCollection services)
        {
            throw new Exception("Services are not initialized");
        }

        services.RegisterServices();
        services.RegisterViewModels();

        var serviceProvider = services.BuildServiceProvider();
        ServiceProvider = serviceProvider;
        
        // Initialize converters that need DI
        var localizationService = serviceProvider.GetRequiredService<ILocalizationService>();
        Converters.ModifierToTextConverter.Initialize(localizationService);
        Converters.ConsciousnessStatusConverter.Initialize(localizationService);
        
        var avaloniaResourcesLocator = serviceProvider.GetRequiredService<IAvaloniaResourcesLocator>();
        Converters.ComponentStatusBackgroundConverter.Initialize(avaloniaResourcesLocator);
        Converters.EventTypeToBackgroundConverter.Initialize(avaloniaResourcesLocator);
        Converters.ConsciousnessColorConverter.Initialize(avaloniaResourcesLocator);

        INavigationService navigationService;

        MainMenuViewModel? viewModel;
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                navigationService = new NavigationService(desktop, serviceProvider);
                RegisterViews(navigationService);
                viewModel = navigationService.GetViewModel<MainMenuViewModel>();
                desktop.MainWindow = new MainWindow
                {
                    Content = new MainMenuView()
                    {
                        ViewModel = viewModel
                    }
                };

                break;
            case ISingleViewApplicationLifetime singleViewPlatform:
                var mainViewWrapper = new ContentControl();
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
        }
        else
        {
            navigationService.RegisterViews(typeof(StartNewGameViewWide), typeof(StartNewGameViewModel));
            navigationService.RegisterViews(typeof(JoinGameViewWide), typeof(JoinGameViewModel));
        }

        // Register views that are the same for all platforms
        navigationService.RegisterViews(typeof(BattleMapView), typeof(BattleMapViewModel));
        navigationService.RegisterViews(typeof(EndGameView), typeof(EndGameViewModel));
        navigationService.RegisterViews(typeof(AboutView), typeof(AboutViewModel));
    }
    private bool IsMobile()
    {
        return OperatingSystem.IsIOS() || OperatingSystem.IsAndroid();
    }
}