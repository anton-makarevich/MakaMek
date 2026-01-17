using Microsoft.Extensions.Options;
using MakaMek.Tools.BotContainer.Configuration;
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

    private ClientGame? _clientGame;
    private IPlayer? _botPlayer;

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
        ILogger<IntegrationBotService> logger)
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
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting Integration Bot Service...");
            
            await InitializeConnection();
            
            _logger.LogInformation("Bot Service started and waiting for game events.");
            
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
        _commandPublisher.Subscribe(HandleServerCommand);

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

        _botManager.Initialize(_clientGame);
        
        // Request lobby status to update the local state
        _clientGame.RequestLobbyStatus(new RequestGameLobbyStatusCommand
        {
            GameOriginId = _clientGame.Id
        });
    }

    private void HandleServerCommand(IGameCommand command)
    {
        try
        {
            switch (command)
            {
                case RequestGameLobbyStatusCommand lobbyCmd:
                    // When we receive lobby status, join the game
                    _logger.LogInformation("Received lobby status. Joining game...");
                    JoinGame();
                    break;
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

    private async void JoinGame()
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

            var availableUnits = await _unitsLoader.LoadUnits();
            if (availableUnits.Count != 0)
            {
                 // Pick based on config names if possible, else random
                 foreach(var cfg in _config.Units)
                 {
                     var matchingUnits = availableUnits.Where(u => u.Chassis.Contains(cfg, StringComparison.OrdinalIgnoreCase) || u.Model.Contains(cfg, StringComparison.OrdinalIgnoreCase)).ToList();
                     if (matchingUnits.Count != 0)
                     {
                         units.Add(matchingUnits.First());
                     }
                 }
                 
                 // Fallback if no specific matches
                 if (units.Count == 0)
                 {
                     units.AddRange(availableUnits.Take(_config.Units.Count > 0 ? _config.Units.Count : 1));
                 }
            }
            
            foreach (var unit in units)
            {
                 pilotAssignments.Add(new PilotAssignmentData
                 {
                     UnitId = unit.Id ?? Guid.NewGuid(),
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
}
