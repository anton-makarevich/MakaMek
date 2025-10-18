using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.ResourceProviders;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.Transport.Rx;

namespace Sanet.MakaMek.Avalonia.DI;

public static class CoreServices
{
    public static void RegisterServices(this IServiceCollection services)
    {
        // Register unit caching service with stream providers
        services.AddSingleton<IUnitCachingService,UnitCachingService>(sp =>
        {
            var cachingService = sp.GetService<IFileCachingService>();
            var streamProviders = new List<IResourceStreamProvider>
            {
                new GitHubResourceStreamProvider("mmux",
                    "https://api.github.com/repos/anton-makarevich/MakaMek/contents/data/units/mechs",
                    cachingService!
                    )
            };
            return new UnitCachingService(streamProviders);
        });

        // Register both image services
        services.AddSingleton<AvaloniaAssetImageService>();
        services.AddSingleton<CachedImageService>();

        // Register a hybrid service that routes to the appropriate underlying service
        services.AddSingleton<IImageService, HybridImageService>();

        // Register map preview renderer
        services.AddSingleton<IMapPreviewRenderer, SkiaMapPreviewRenderer>();

        services.AddSingleton<IUnitsLoader, MmuxUnitsLoader>();

        services.AddSingleton<ILocalizationService, FakeLocalizationService>();
        services.AddSingleton<IAvaloniaResourcesLocator, AvaloniaResourcesLocator>();

        // Register RxTransportPublisher for local players
        services.AddSingleton<RxTransportPublisher>();

        // Register CommandTransportAdapter with just the RxTransportPublisher initially
        // The network publisher will be added dynamically when needed
        services.AddTransient<CommandTransportAdapter>(sp =>
            new CommandTransportAdapter(sp.GetRequiredService<RxTransportPublisher>()));

        services.AddTransient<ICommandPublisher, CommandPublisher>();
        services.AddSingleton<IRulesProvider, ClassicBattletechRulesProvider>();
        services.AddSingleton<IComponentProvider, ClassicBattletechComponentProvider>();
        services.AddSingleton<IMechFactory, MechFactory>();
        services.AddSingleton<IDiceRoller, RandomDiceRoller>();
        services.AddSingleton<IDamageTransferCalculator, DamageTransferCalculator>();
        services.AddSingleton<ICriticalHitsCalculator, CriticalHitsCalculator>();
        services.AddSingleton<IConsciousnessCalculator, ConsciousnessCalculator>();
        services.AddSingleton<IHeatEffectsCalculator, HeatEffectsCalculator>();
        services.AddSingleton<IToHitCalculator, ToHitCalculator>();
        services.AddSingleton<IPilotingSkillCalculator, PilotingSkillCalculator>();
        services.AddSingleton<IFallingDamageCalculator, FallingDamageCalculator>();
        services.AddSingleton<IFallProcessor, FallProcessor>();
        services.AddSingleton<IGameFactory, GameFactory>();
        services.AddSingleton<IBattleMapFactory, BattleMapFactory>();
        services.AddSingleton<ITransportFactory, SignalRTransportFactory>();
        services.AddSingleton<IGameManager, GameManager>();
        services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();
    }
    public static void RegisterViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainMenuViewModel>();
        services.AddTransient<StartNewGameViewModel>();
        services.AddTransient<JoinGameViewModel>();
        services.AddTransient<BattleMapViewModel>();
        services.AddTransient<EndGameViewModel>();
        services.AddTransient<AboutViewModel>();
    }
}