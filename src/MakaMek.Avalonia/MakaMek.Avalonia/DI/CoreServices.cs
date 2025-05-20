using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels;
using Sanet.Transport.Rx;

namespace Sanet.MakaMek.Avalonia.DI;

public static class CoreServices
{
    public static void RegisterServices(this IServiceCollection services)
    {
        services.AddSingleton<IImageService, AvaloniaAssetImageService>();
        services.AddSingleton<ILocalizationService, FakeLocalizationService>();
        
        // Register RxTransportPublisher for local players
        services.AddSingleton<RxTransportPublisher>();

        // Register CommandTransportAdapter with just the RxTransportPublisher initially
        // The network publisher will be added dynamically when needed
        services.AddTransient<CommandTransportAdapter>(sp => 
            new CommandTransportAdapter(sp.GetRequiredService<RxTransportPublisher>()));
            
        services.AddTransient<ICommandPublisher, CommandPublisher>();
        services.AddSingleton<IRulesProvider, ClassicBattletechRulesProvider>();
        services.AddSingleton<IMechFactory, MechFactory>();
        services.AddSingleton<IDiceRoller, RandomDiceRoller>();
        services.AddSingleton<ICriticalHitsCalculator, CriticalHitsCalculator>();
        services.AddSingleton<IToHitCalculator, ToHitCalculator>();
        services.AddSingleton<IMechDataProvider, MtfDataProvider>();
        services.AddSingleton<IUnitsLoader, EmbeddedResourcesUnitsLoader>();
        services.AddSingleton<IGameFactory, GameFactory>();
        services.AddSingleton<IBattleMapFactory, BattleMapFactory>();
        services.AddSingleton<ITransportFactory, SignalRTransportFactory>();
        services.AddSingleton<IGameManager, GameManager>();
        services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();
    }
    public static void RegisterViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainMenuViewModel, MainMenuViewModel>();
        services.AddTransient<StartNewGameViewModel, StartNewGameViewModel>();
        services.AddTransient<JoinGameViewModel, JoinGameViewModel>();
        services.AddTransient<BattleMapViewModel, BattleMapViewModel>();
    }
}