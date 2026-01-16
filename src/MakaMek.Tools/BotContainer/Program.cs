using MakaMek.Tools.BotContainer.Configuration;
using MakaMek.Tools.BotContainer.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.ResourceProviders;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.Transport.Rx;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection("BotConfiguration"));

// Core Services Registration (Adapted from Avalonia CoreServices)

// File Caching
builder.Services.AddSingleton<IFileCachingService>(sp => 
    new FileSystemCachingService(sp.GetRequiredService<ILoggerFactory>()));

// Unit Loading (using GitHub/MMUX like Avalonia)
builder.Services.AddSingleton<IUnitCachingService, UnitCachingService>(sp =>
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
builder.Services.AddSingleton<IUnitsLoader, MmuxUnitsLoader>();

// Transport
builder.Services.AddSingleton<RxTransportPublisher>(); // For local loopback logic
builder.Services.AddTransient<CommandTransportAdapter>(sp =>
{
    var rxPublisher = sp.GetRequiredService<RxTransportPublisher>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new CommandTransportAdapter(loggerFactory, rxPublisher);
});
builder.Services.AddTransient<ICommandPublisher, CommandPublisher>();
builder.Services.AddSingleton<ITransportFactory, SignalRTransportFactory>();

// Game Core
builder.Services.AddSingleton<IRulesProvider, ClassicBattletechRulesProvider>();
builder.Services.AddSingleton<IComponentProvider, ClassicBattletechComponentProvider>();
builder.Services.AddSingleton<IMechFactory, MechFactory>();
builder.Services.AddSingleton<IDiceRoller, RandomDiceRoller>();
builder.Services.AddSingleton<IDamageTransferCalculator, DamageTransferCalculator>();
builder.Services.AddSingleton<ICriticalHitsCalculator, CriticalHitsCalculator>();
builder.Services.AddSingleton<IConsciousnessCalculator, ConsciousnessCalculator>();
builder.Services.AddSingleton<IHeatEffectsCalculator, HeatEffectsCalculator>();
builder.Services.AddSingleton<IToHitCalculator, ToHitCalculator>();
builder.Services.AddSingleton<IPilotingSkillCalculator, PilotingSkillCalculator>();
builder.Services.AddSingleton<IFallingDamageCalculator, FallingDamageCalculator>();
builder.Services.AddSingleton<IFallProcessor, FallProcessor>();
builder.Services.AddSingleton<IGameFactory, GameFactory>();
builder.Services.AddSingleton<IBattleMapFactory, BattleMapFactory>();
builder.Services.AddSingleton<IGameManager, GameManager>();
builder.Services.AddSingleton<IHashService, HashService>();

// Bot Specific
builder.Services.AddSingleton<IDispatcherService, HeadlessDispatcherService>();
builder.Services.AddSingleton<IBotManager, BotManager>();

// Hosted Service
builder.Services.AddHostedService<IntegrationBotService>();

var app = builder.Build();

app.UseHttpsRedirection();

// Basic health check endpoint
app.MapGet("/health", () => "Integration Bot Container Running");

app.Run();
