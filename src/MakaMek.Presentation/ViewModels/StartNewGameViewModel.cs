using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Cryptography;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class StartNewGameViewModel : NewGameViewModel, IDisposable
{
    private readonly IGameManager _gameManager;
    private readonly IBattleMapFactory _mapFactory;
    private readonly ILogger<StartNewGameViewModel> _logger;

    public StartNewGameViewModel(
        IGameManager gameManager,
        IUnitsLoader unitsLoader,
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        IBattleMapFactory mapFactory,
        IFileCachingService cachingService,
        IMapPreviewRenderer mapPreviewRenderer,
        IMapResourceProvider mapResourceProvider,
        IHashService hashService,
        IBotManager botManager,
        ILogger<StartNewGameViewModel> logger)
        : base(rulesProvider,
            unitsLoader,
            commandPublisher,
            toHitCalculator,
            pilotingSkillCalculator,
            consciousnessCalculator,
            heatEffectsCalculator,
            dispatcherService,
            gameFactory,
            cachingService,
            hashService,
            botManager,
            mechFactory,
            logger)
    {
        _gameManager = gameManager;
        _mapFactory = mapFactory;
        _logger = logger;
        MapConfig = new MapConfigViewModel(mapPreviewRenderer, mapFactory, mapResourceProvider, logger);
        AddPlayerCommand = new AsyncCommand(() => AddPlayer());
        AddBotCommand = new AsyncCommand(()=>AddPlayer(controlType: PlayerControlType.Bot));
    }

    public async Task InitializeLobbyAndSubscribe()
    {
        await _gameManager.InitializeLobby();
        _commandPublisher.Subscribe(HandleServerCommand);
        // Use the factory to create the ClientGame
        _localGame = _gameFactory.CreateClientGame(
            _rulesProvider,
            _mechFactory,
            _commandPublisher,
            _toHitCalculator,
            _pilotingSkillCalculator,
            _consciousnessCalculator,
            _heatEffectsCalculator,
            _mapFactory,
            _hashService);

        // Initialize BotManager with the ClientGame and DecisionEngineProvider
        var decisionEngineProvider = new DecisionEngineProvider(_localGame);
        _botManager.Initialize(_localGame, decisionEngineProvider);

        // Update server IP initially if needed
        NotifyPropertyChanged(nameof(ServerIpAddress));
    }

    // Implementation of the abstract method from the base class
    protected override Task HandleCommandInternal(IGameCommand command)
    {
        switch (command)
        {
            // Handle player joining (potentially echo of local or a remote player)
            case UpdatePlayerStatusCommand statusCmd:
                var playerWithStatusUpdate = _players.FirstOrDefault(p => p.Player.Id == statusCmd.PlayerId);
                if (playerWithStatusUpdate != null && statusCmd.GameOriginId == _gameManager.ServerGameId)
                {
                    // Update player status
                    playerWithStatusUpdate.Player.Status = statusCmd.PlayerStatus;
                    playerWithStatusUpdate.RefreshStatus();
                    NotifyPropertyChanged(nameof(CanStartGame));
                }
                break;
            
            case JoinGameCommand joinCmd:
                var existingPlayerVm = _players.FirstOrDefault(p => p.Player.Id == joinCmd.PlayerId);
                if (existingPlayerVm != null)
                {
                    // Player exists - likely the echo for a local player who just clicked Join
                    if (existingPlayerVm.IsLocalPlayer && joinCmd.GameOriginId == _gameManager.ServerGameId)
                    {
                        // Server accepted the join request
                        existingPlayerVm.Player.Status = PlayerStatus.Joined;
                        existingPlayerVm.RefreshStatus();
                        NotifyPropertyChanged(nameof(CanStartGame));
                    }
                    // Else: Remote player sending join again? Ignore.
                }
                else
                {
                    // Player doesn't exist - must be a remote player joining
                    var remotePlayer = new Player(joinCmd.PlayerId,
                        joinCmd.PlayerName,
                        PlayerControlType.Remote,
                        joinCmd.Tint);
                    var remotePlayerVm = new PlayerViewModel(
                        remotePlayer,
                        isLocalPlayer: false, // Mark as remote
                        _ => {}, // No join action needed for remote
                        _ => {}, // No set ready action needed for remote
                        _ => Task.CompletedTask, // No show units action needed for remote
                        () => NotifyPropertyChanged(nameof(CanStartGame)));
                    
                    remotePlayerVm.AddUnits(joinCmd.Units, joinCmd.PilotAssignments); // Add units received from command
                    _players.Add(remotePlayerVm);
                    NotifyPropertyChanged(nameof(CanAddPlayer));
                    NotifyPropertyChanged(nameof(CanStartGame));
                }
                break;
        }
        return Task.CompletedTask; 
    }
    
    public MapConfigViewModel MapConfig { get; }

    public bool CanStartGame => Players.Count > 0 && Players.All(p => p.Units.Count > 0 && p.Player.Status == PlayerStatus.Ready);
    
    /// <summary>
    /// Gets the server address if LAN is running
    /// </summary>
    public string ServerIpAddress
    {
        get
        {
            var serverUrl = _gameManager.GetLanServerAddress();
            if (string.IsNullOrEmpty(serverUrl))
                return "LAN Disabled..."; // Indicate status
            try
            {
                // Extract host from the URL
                var uri = new Uri(serverUrl);
                return $"{uri.Host}"; // Display only Host name/IP
            }
            catch
            {
                return "Invalid Address"; 
            }
        }
    }
    
    public bool CanStartLanServer => _gameManager.CanStartLanServer;

    public ICommand StartGameCommand => new AsyncCommand(async () =>
    {
        if (MapConfig.Map == null) return;
        // Use the map generated by MapConfigViewModel to ensure preview and game map are identical
        var map = MapConfig.Map;

        // Set BattleMap on GameManager/ServerGame (propagates to clients via the command system)
        _gameManager.SetBattleMap(map);

        // Host Client for local player(s)
        var battleMapViewModel = NavigationService.GetNewViewModel<BattleMapViewModel>();
        if (battleMapViewModel == null)
        {
            throw new Exception("BattleMapViewModel is not registered");
        }
        battleMapViewModel.Game = _localGame;

        // Navigate to BattleMap view
        await NavigationService.NavigateToViewModelAsync(battleMapViewModel);
    });
    
    // Implementation of template method from base class
    protected override PlayerViewModel CreatePlayerViewModel(Player player, bool isDefaultPlayer = false)
    {
        return new PlayerViewModel(
            player,
            isLocalPlayer: true,
            PublishJoinCommand,
            PublishSetReadyCommand,
            ShowAvailableUnitsTable,
            () => NotifyPropertyChanged(nameof(CanStartGame)),
            isDefaultPlayer
                ? OnDefaultPlayerNameChanged
                : null,
            isDefaultPlayer);
    }
    
    // Override the base AddPlayer to add additional notification
    protected override Task AddPlayer(
        PlayerData? playerData = null,
        PlayerControlType controlType = PlayerControlType.Human)
    {
        var result = base.AddPlayer(playerData, controlType);
        NotifyPropertyChanged(nameof(CanStartGame)); // CanStartGame might be false until units are added
        return result;
    }
    
    // Implementation of abstract property from base class
    public override bool CanAddPlayer => _players.Count < 4; // Limit to 4 players for now
    
    // Implementation of abstract property from base class
    public override bool CanPublishCommands => true; // TODO: is it actually always true?

    public void Dispose()
    {
        MapConfig.Dispose();
        GC.SuppressFinalize(this);
    }

    public override void AttachHandlers()
    {
        base.AttachHandlers();
        InitializeLobbyAndSubscribe().SafeFireAndForget(
            ex => _logger.LogError(ex, "Error initializing lobby"));
    }
}