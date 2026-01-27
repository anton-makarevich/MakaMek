using ModelContextProtocol.Protocol;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Services.Logging.Factories;
using Sanet.MakaMek.Core.Services.ResourceProviders;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Tools.BotContainer.Configuration;
using Sanet.MakaMek.Tools.BotContainer.Models.Mcp.Tools;
using Sanet.MakaMek.Tools.BotContainer.Services;
using Sanet.Transport.Rx;

namespace Sanet.MakaMek.Tools.BotContainer.DependencyInjection;

public static class BotContainerServices
{
    public static void AddBotContainerServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<BotConfiguration>(configuration.GetSection("BotConfiguration"));
        services.Configure<BotAgentConfiguration>(configuration.GetSection("BotAgent"));

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // File Caching
        services.AddSingleton<IFileCachingService, FileSystemCachingService>(sp => 
            new FileSystemCachingService(sp.GetRequiredService<ILoggerFactory>()));

        // Command Logger
        services.AddSingleton<ICommandLoggerFactory, FileCommandLoggerFactory>();

        // Unit Loading (using GitHub/MMUX like Avalonia)
        services.AddSingleton<IUnitCachingService, UnitCachingService>(sp =>
        {
            var cachingService = sp.GetRequiredService<IFileCachingService>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var streamProviders = new List<IResourceStreamProvider>
            {
                new GitHubResourceStreamProvider("mmux",
                    "https://api.github.com/repos/anton-makarevich/MakaMek/contents/data/units/mechs",
                    cachingService,
                    loggerFactory
                )
            };
            return new UnitCachingService(streamProviders, loggerFactory);
        });
        services.AddSingleton<IUnitsLoader, MmuxUnitsLoader>();

        // Localization
        services.AddSingleton<ILocalizationService, FakeLocalizationService>();

        // Transport
        services.AddSingleton<RxTransportPublisher>(); // For local loopback logic
        services.AddTransient<CommandTransportAdapter>(sp =>
        {
            var rxPublisher = sp.GetRequiredService<RxTransportPublisher>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new CommandTransportAdapter(loggerFactory, rxPublisher);
        });
        services.AddTransient<ICommandPublisher, CommandPublisher>();
        services.AddSingleton<ITransportFactory, SignalRTransportFactory>();

        // Game Core
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
        services.AddSingleton<IGameManager, GameManager>();
        services.AddSingleton<IHashService, HashService>();

        // Bot Specific
        services.AddSingleton<IDispatcherService, HeadlessDispatcherService>();

        // LLM Bot Agent Integration
        services.AddHttpClient<BotAgentClient>();
        services.AddSingleton<IBotManager, BotManager>();

        // Hosted Service
        services.AddHostedService<IntegrationBotService>();

        // MCP Server
        services.AddSingleton<IGameStateProvider, GameStateProvider>();
        services.AddMcpServer((options) =>
            {
                options.ServerInfo= new Implementation
                {
                    Name = "MakaMek MCP Server",
                    Version = "0.1.0"
                };
            })
            .WithHttpTransport((options) =>
            {
                options.Stateless = true;
            })
            .WithTools<DeploymentTools>()
            .WithTools<MovementTools>()
            .WithTools<WeaponsAttackTools>();
    }
}
