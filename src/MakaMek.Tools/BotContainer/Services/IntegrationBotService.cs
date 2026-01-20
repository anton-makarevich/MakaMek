using Microsoft.Extensions.Options;
using MakaMek.Tools.BotContainer.Configuration;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Data.Units;

namespace MakaMek.Tools.BotContainer.Services;

public class IntegrationBotService : BackgroundService
{
    private readonly BotConfiguration _config;
    private readonly ITransportFactory _transportFactory;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IGameFactory _gameFactory;
    private readonly IBotManager _botManager;
    private readonly IRulesProvider _rulesProvider;
    private readonly Sanet.MakaMek.Core.Utils.IMechFactory _mechFactory;
    private readonly IUnitsLoader _unitsLoader;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly IPilotingSkillCalculator _pilotingSkillCalculator;
    private readonly IConsciousnessCalculator _consciousnessCalculator;
    private readonly IHeatEffectsCalculator _heatEffectsCalculator;
    private readonly IBattleMapFactory _mapFactory;
    private readonly IHashService _hashService;
    private readonly ILogger<IntegrationBotService> _logger;
    private readonly BotAgentClient _botAgentClient;
    private readonly IOptions<BotAgentConfiguration> _botAgentConfig;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServer _server;
    private IDisposable? _gameCommandsSubscription;

    private ClientGame? _clientGame;
    private IPlayer? _botPlayer;
    
    private List<UnitData> _availableUnits = [];

    private readonly IGameStateProvider _gameStateProvider;

    public IntegrationBotService(
        IOptions<BotConfiguration> config,
        ITransportFactory transportFactory,
        ICommandPublisher commandPublisher,
        IGameFactory gameFactory,
        IBotManager botManager,
        IRulesProvider rulesProvider,
        Sanet.MakaMek.Core.Utils.IMechFactory mechFactory,
        IUnitsLoader unitsLoader,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IBattleMapFactory mapFactory,
        IHashService hashService,
        ILogger<IntegrationBotService> logger,
        BotAgentClient botAgentClient,
        IOptions<BotAgentConfiguration> botAgentConfig,
        ILoggerFactory loggerFactory,
        IServer server,
        IGameStateProvider gameStateProvider)
    {
        _config = config.Value;
        _transportFactory = transportFactory;
        _commandPublisher = commandPublisher;
        _gameFactory = gameFactory;
        _botManager = botManager;
        _rulesProvider = rulesProvider;
        _mechFactory = mechFactory;
        _unitsLoader = unitsLoader;
        _toHitCalculator = toHitCalculator;
        _pilotingSkillCalculator = pilotingSkillCalculator;
        _consciousnessCalculator = consciousnessCalculator;
        _heatEffectsCalculator = heatEffectsCalculator;
        _mapFactory = mapFactory;
        _hashService = hashService;
        _logger = logger;
        _botAgentClient = botAgentClient;
        _botAgentConfig = botAgentConfig;
        _loggerFactory = loggerFactory;
        _server = server;
        _gameStateProvider = gameStateProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Loading units...");
            _availableUnits = await _unitsLoader.LoadUnits();

            _logger.LogInformation("Starting Integration Bot Service...");
            await InitializeConnection();
            
            _logger.LogInformation("Bot Service started and waiting for game events.");
            
            // Request lobby status to update the local state
            _clientGame?.RequestLobbyStatus(new RequestGameLobbyStatusCommand
            {
                GameOriginId = _clientGame.Id
            });
            
            // Join the game
            JoinGame();

            // Keep the service alive
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Integration Bot Service");
        }
    }

    private async Task InitializeConnection()
    {
        _logger.LogInformation("Connecting to server at {ServerUrl}...", _config.ServerUrl);

        var adapter = _commandPublisher.Adapter;
        adapter.ClearPublishers();

        var client = await _transportFactory.CreateAndStartClientPublisher(_config.ServerUrl);
        adapter.AddPublisher(client);

        _clientGame = _gameFactory.CreateClientGame(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _mapFactory,
            _hashService);

        _gameStateProvider.ClientGame = _clientGame;

        // Initialize BotManager with LLM-enabled DecisionEngineProvider
        var mcpServerUrl = ConstructMcpServerUrl();
        _logger.LogInformation("Using MCP server URL: {McpServerUrl}", mcpServerUrl);
        
        var decisionEngineProvider = new LlmDecisionEngineProvider(
            _clientGame,
            _botAgentClient,
            _botAgentConfig,
            mcpServerUrl,
            _loggerFactory);
        _botManager.Initialize(_clientGame, decisionEngineProvider);
        
        _gameCommandsSubscription = _clientGame.Commands
            .Subscribe(HandleGameCommand);
    }

    private void HandleGameCommand(IGameCommand command)
    {
        if (command.GameOriginId == _clientGame?.Id) return;
        try
        {
            switch (command)
            {
                case JoinGameCommand joinCmd:
                    // Log other players joining
                    if (joinCmd.PlayerId != _botPlayer?.Id)
                    {
                        _logger.LogInformation("Player {PlayerName} joined the game.", joinCmd.PlayerName);
                    }
                    else
                    {
                        // This is the confirmation that our bot joined successfully
                        _logger.LogInformation("Bot successfully joined the game. Setting status to Ready.");
                        SetReady();
                    }
                    break;
                case UpdatePlayerStatusCommand statusCmd:
                    if (statusCmd.PlayerId == _botPlayer?.Id && statusCmd.PlayerStatus == PlayerStatus.Ready)
                    {
                        _logger.LogInformation("Bot is ready.");
                    }
                    break;
                case ChangePhaseCommand phaseCmd:
                    _logger.LogInformation("Phase changed to {Phase}", phaseCmd.Phase);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling server command {CommandType}", command.GetType().Name);
        }
    }

    private void JoinGame()
    {
        try
        {
            _logger.LogInformation("Joining game as {BotName}...", _config.BotName);

            var playerData = new PlayerData
            {
                Id = Guid.NewGuid(),
                Name = _config.BotName,
                Tint = _config.BotTeam
            };

            _botPlayer = new Player(playerData, PlayerControlType.Bot, Guid.NewGuid());
            _botManager.AddBot(_botPlayer); // Register with BotManager

            var units = new List<UnitData>();
            var pilotAssignments = new List<PilotAssignmentData>();

            if (_availableUnits.Count != 0)
            {
                // Pick based on config names if possible, else random
                foreach (var cfg in _config.Units)
                {
                    var matchingUnits = _availableUnits.Where(u =>
                        u.Chassis.Contains(cfg, StringComparison.OrdinalIgnoreCase) ||
                        u.Model.Contains(cfg, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matchingUnits.Count != 0)
                    {
                        units.Add(matchingUnits.First() with { Id = Guid.NewGuid() });
                    }
                }
            }

            foreach (var unit in units)
            {
                if (unit.Id == null || unit.Id == Guid.Empty)
                {
                    _logger.LogWarning("Unit {UnitModel} has no ID. Generating a new one.", unit.Model);
                    continue;
                }

                pilotAssignments.Add(new PilotAssignmentData
                {
                    UnitId = unit.Id.Value,
                    PilotData = PilotData.CreateDefaultPilot("BotPilot", _botPlayer.Id.ToString())
                });
            }

            _clientGame?.JoinGameWithUnits(_botPlayer, units, pilotAssignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join game");
        }
    }

    private void SetReady()
    {
        if (_clientGame == null || _botPlayer == null) return;

        var readyCommand = new UpdatePlayerStatusCommand
        {
            GameOriginId = _clientGame.Id,
            PlayerId = _botPlayer.Id,
            PlayerStatus = PlayerStatus.Ready
        };
        _clientGame.SetPlayerReady(readyCommand);
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Integration Bot Service...");
        _gameStateProvider.ClientGame = null;
        _gameCommandsSubscription?.Dispose();
        _gameCommandsSubscription = null;
        _clientGame?.Dispose();
        _clientGame = null;
        _botPlayer = null;
        await base.StopAsync(cancellationToken);
    }

    private string ConstructMcpServerUrl()
    {
        var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses == null || !addresses.Any())
        {
            _logger.LogWarning("No server addresses found, falling back to localhost:5000");
            return "http://localhost:5000/mcp";
        }

        // Prefer HTTP over HTTPS, and prefer non-loopback addresses
        var preferredAddress = addresses
            .Where(addr => addr.StartsWith("http://"))
            .FirstOrDefault(addr => !addr.Contains("localhost") && !addr.Contains("127.0.0.1"))
            ?? addresses.FirstOrDefault(addr => addr.StartsWith("http://"))
            ?? addresses.First();

        return $"{preferredAddress.TrimEnd('/')}/mcp";
    }
}
