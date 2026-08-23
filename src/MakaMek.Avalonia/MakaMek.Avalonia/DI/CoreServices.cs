using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Avalonia.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Mechanics.WeaponAttack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Services.Transport.Relay;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Services;
using Sanet.MakaMek.Presentation.ViewModels;
using Sanet.MakaMek.Services;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Services.Avalonia;
using Sanet.MakaMek.Services.ResourceProviders;
using Sanet.Transport;
using Sanet.Transport.Rx;
using Sanet.Transport.SignalR.Client.Factories;

namespace Sanet.MakaMek.Avalonia.DI;

public static class CoreServices
{
    public static void RegisterServices(this IServiceCollection services)
    {
        // Register unit caching service with stream providers (from MakaMek.Assets)
        services.AddSingleton<IUnitCachingService>(sp =>
        {
            var cachingService = sp.GetRequiredService<IFileCachingService>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var streamProviders = new List<IResourceStreamProvider>
            {
                new GitHubResourceStreamProvider("mmux",
                    "https://api.github.com/repos/anton-makarevich/MakaMek/contents/data/units/mechs",
                    cachingService,
                    loggerFactory.CreateLogger<GitHubResourceStreamProvider>()
                )
            };
            return new UnitCachingService(streamProviders, loggerFactory);
        });

        // Register terrain caching service with stream providers
        services.AddSingleton<ITerrainAssetService>(sp =>
        {
            var cachingService = sp.GetRequiredService<IFileCachingService>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var streamProviders = new List<IResourceStreamProvider>
            {
                new GitHubResourceStreamProvider("mmtx",
                    "https://api.github.com/repos/anton-makarevich/MakaMek/contents/data/hexes/biomes",
                    cachingService,
                    loggerFactory.CreateLogger<GitHubResourceStreamProvider>()
                )
            };
            return new TerrainCachingService(streamProviders, loggerFactory);
        });

        // Register both image services
        services.AddSingleton<AvaloniaAssetImageService>(_ =>
            new AvaloniaAssetImageService("avares://Sanet.MakaMek.Avalonia/Assets"));
        services.AddSingleton<CachedImageService>();

        // Register a hybrid service that routes to the appropriate underlying service
        services.AddSingleton<IImageService>(sp =>
        {
            var avaloniaService = sp.GetRequiredService<AvaloniaAssetImageService>();
            var cachedService = sp.GetRequiredService<CachedImageService>();
            var terrainService = sp.GetService<ITerrainAssetService>();
            return new HybridImageService(avaloniaService, cachedService, terrainService);
        });

        // Register map preview renderer
        services.AddSingleton<IMapPreviewRenderer, SkiaMapPreviewRenderer>();

        services.AddSingleton<IMapResourceProvider>(sp =>
            new EmbeddedMapResourceProvider(
                sp.GetRequiredService<ILogger<EmbeddedMapResourceProvider>>(),
                new AssemblyResourceStreamProvider("json", typeof(App).Assembly)));
        services.AddSingleton<IFileService, AvaloniaFileService>();
        services.AddSingleton<IClipboardService, AvaloniaClipboardService>();

        services.AddSingleton<IPdfExportService, PdfExportService>();

        services.AddSingleton<IUnitsLoader, MmuxUnitsLoader>();

        services.AddSingleton<ILocalizationService, FakeLocalizationService>();
        services.AddSingleton<IAvaloniaResourcesLocator, AvaloniaResourcesLocator>();

        // Register RxTransportPublisher for local players
        services.AddSingleton<RxTransportPublisher>();

        // Register CommandTransportAdapter with just the RxTransportPublisher initially
        // The network publisher will be added dynamically when needed. Both the adapter and the
        // command publisher are singletons so GameManager, GameConnector, ViewModels and the
        // local games all share ONE command pipeline: publishers added by GameConnector (LAN /
        // relay) must be visible to the adapter that games publish through and subscribe to.
        // Transient lifetimes silently split the pipeline — commands never reach the network.
        services.AddSingleton<CommandTransportAdapter>(sp =>
        {
            var rxPublisher = sp.GetRequiredService<RxTransportPublisher>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new CommandTransportAdapter(loggerFactory, rxPublisher);
        });

        services.AddSingleton<ICommandPublisher, CommandPublisher>();
        services.AddSingleton<IRulesProvider, TotalWarfareRulesProvider>();
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
        services.AddSingleton<IWeaponAttackResolver, WeaponAttackResolver>();
        services.AddSingleton<IGameFactory, GameFactory>();
        services.AddSingleton<IBattleMapFactory, BattleMapFactory>();
        services.AddSingleton<ITerrainBitmaskService, TerrainBitmaskService>();
        services.AddSingleton<ITransportFactory, SignalRTransportFactory>();
        services.AddSingleton<IGameManager, GameManager>();
        services.AddTransient<IGameConnector, GameConnector>();
        services.AddSingleton<IDispatcherService, AvaloniaDispatcherService>();
        services.AddSingleton<IHashService, HashService>();
        services.AddSingleton<IBotManager, BotManager>();
        services.AddSingleton<IPlatformService, AvaloniaPlatformService>();

        // Shared in-memory configuration source that populates the RelayClient section for all heads.
        // Defaults come from build-time-embedded Demo hub values (DemoHubDefaults); override via
        // MAKAMEK_RELAY_BASE_URL / MAKAMEK_RELAY_API_KEY for local development.
        services.AddSingleton<IConfiguration>(_ =>
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [RelayClientOptions.SectionName + ":BaseUrl"] = DemoHubDefaults.BaseUrl,
                    [RelayClientOptions.SectionName + ":ApiKey"] = DemoHubDefaults.ApiKey
                })
                .Build();
            return configuration;
        });

        // Relay publisher factory — shared, creates relay publishers for online hosts.
        services.AddSingleton<IPublisherFactory, RelayPublisherFactory>();

        // Relay hub configuration — seeds the built-in Demo hub from RelayClientOptions
        // (MAKAMEK_RELAY_BASE_URL / MAKAMEK_RELAY_API_KEY) and persists user-defined hubs.
        services.AddSingleton<IRelayHubConfigurationProvider, RelayHubConfigurationProvider>();

        // Relay room client — reads the active hub configuration from IRelayHubConfigurationProvider
        // on every request instead of a frozen options snapshot.
        services.AddOptions<RelayClientOptions>()
            .BindConfiguration(RelayClientOptions.SectionName);
        services.AddHttpClient<IRelayRoomClient, RelayRoomClient>();
    }

    public static void RegisterViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainMenuViewModel>();
        services.AddTransient<StartNewGameViewModel>();
        services.AddTransient<JoinGameViewModel>();
        services.AddTransient<BattleMapViewModel>();
        services.AddTransient<EndGameViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}