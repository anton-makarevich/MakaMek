using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Bots.Models;
using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.Models.Logger;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class JoinGameViewModel : NewGameViewModel, IDisposable
{
    private readonly ITransportFactory _transportFactory;

    public JoinGameViewModel(
        IUnitsLoader unitsLoader,
        ICommandPublisher commandPublisher,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        ITransportFactory transportFactory,
        IFileCachingService cachingService,
        IBotManager botManager,
        ILogger<JoinGameViewModel> logger,
        IMechFactory mechFactory)
        : base(unitsLoader,
            commandPublisher,
            dispatcherService,
            gameFactory,
            cachingService,
            botManager,
            mechFactory,
            logger)
    {
        _transportFactory = transportFactory;

        AddPlayerCommand = new AsyncCommand(() => AddPlayer());
        AddBotCommand = new AsyncCommand(()=>AddPlayer(controlType: PlayerControlType.Bot));
        ConnectCommand = new AsyncCommand(ConnectToServer, (_)=>CanConnect);
    }

    // Implementation of the abstract method from a base class
    protected override async Task HandleCommandInternal(IGameCommand command)
    {
        switch (command)
        {
            case UpdatePlayerStatusCommand statusCmd:
                var playerWithStatusUpdate = _players.FirstOrDefault(p => p.Player.Id == statusCmd.PlayerId);
                if (playerWithStatusUpdate != null) // Simplified check for join view
                {
                    playerWithStatusUpdate.Player.Status = statusCmd.PlayerStatus;
                    playerWithStatusUpdate.RefreshStatus();
                    // Potentially update CanStartGame equivalent if needed
                }
                break;

            case JoinGameCommand joinCmd:
                var existingPlayerVm = _players.FirstOrDefault(p => p.Player.Id == joinCmd.PlayerId);
                if (existingPlayerVm == null) // Add if it's a new remote player
                {
                     var newRemotePlayer = new Player(joinCmd.PlayerId,
                         joinCmd.PlayerName,
                         PlayerControlType.Remote,
                         joinCmd.Tint); 
                     var remotePlayerViewModel = new PlayerViewModel(
                        newRemotePlayer,
                        isLocalPlayer: false,
                        _ => {}, // Remote players don't publish join
                        _ => {}); // No callback for ready state
                     remotePlayerViewModel.AddUnits(joinCmd.Units, joinCmd.PilotAssignments); // Add units received from command
                     _players.Add(remotePlayerViewModel);
                }
                else if (existingPlayerVm.IsLocalPlayer)
                {
                     // Handle join confirmation for local player
                     existingPlayerVm.Player.Status = PlayerStatus.Joined;
                     existingPlayerVm.RefreshStatus();
                }
                break;
                
            case SetBattleMapCommand:
                // Handle navigation to BattleMapViewModel when the battle map is set
                
                // Get the BattleMapViewModel and set the game
                var battleMapViewModel = NavigationService.GetViewModel<BattleMapViewModel>();
                if (battleMapViewModel == null)
                {
                    throw new Exception("BattleMapViewModel is not registered");
                }
                battleMapViewModel.Game = _localGame;
                
                // Navigate to BattleMap view
                await NavigationService.NavigateToViewModelAsync(battleMapViewModel);
                
                break;
        }
        
        // Refresh bindings
        NotifyPropertyChanged(nameof(Players));
    }

    public string ServerIp
    {
        get;
        set
        {
            SetProperty(ref field, value);
            (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanConnect));
            NotifyPropertyChanged(nameof(ServerAddress));
            NotifyPropertyChanged(nameof(CanAddPlayer));
        }
    } = string.Empty;

    public string ServerAddress => $"http://{ServerIp}:2439/makamekhub";

    public bool IsConnected
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            // Update UI based on connection status
            foreach (var player in _players)
            {
                player.RefreshStatus();
            }
        }
    }

    public ICommand ConnectCommand { get; private set; }

    public bool CanConnect => !string.IsNullOrWhiteSpace(ServerIp) && !IsConnected;

    private async Task ConnectToServer()
    {
        if (!CanConnect) return;

        try
        {
            // Get access to the adapter from the command publisher
            var adapter = _commandPublisher.Adapter;
            
            // Clear any existing publishers and prepare for a new connection
            adapter.ClearPublishers();
            
            // Create a network client publisher using the factory and connect
            var client = await _transportFactory.CreateAndStartClientPublisher(ServerAddress);
            adapter.AddPublisher(client);
            _commandPublisher.Subscribe(HandleServerCommand);
            if (_localGame != null)
            {
                _localGame.Dispose();
                _localGame = null;
            }
            _localGame = _gameFactory.CreateClientGame(_commandPublisher);

            _localGame.Logger.LogAttemptedToConnectToServerIp(ServerIp);
            
            // Initialize BotManager with the ClientGame and DecisionEngineProvider
            var decisionEngineProvider = new DecisionEngineProvider(_localGame);
            _botManager.Initialize(_localGame, decisionEngineProvider);

            IsConnected = true;
            _localGame.RequestLobbyStatus(new RequestGameLobbyStatusCommand
            {
                GameOriginId = _localGame.Id
            });
            (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged(); // Disable connect button
            NotifyPropertyChanged(nameof(CanAddPlayer)); // Enable Add Player once connected
        }
        catch (Exception ex)
        {
            _localGame?.Logger.LogError(ex, "Error connecting to server: {Message}", ex.Message);
            IsConnected = false;
        }
    }

    public void Disconnect()
    {
        if (_localGame != null)
        {
            _localGame.Dispose();
            _localGame = null;
        }
        _commandPublisher.Unsubscribe(HandleServerCommand);
        _commandPublisher.Adapter.ClearPublishers();
        IsConnected = false;
        (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        NotifyPropertyChanged(nameof(CanAddPlayer));
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }

    // Implementation of template method from base class
    protected override PlayerViewModel CreatePlayerViewModel(Player player, bool isDefaultPlayer = false)
    {
        return new PlayerViewModel(
            player,
            isLocalPlayer: true,
            PublishJoinCommand,
            PublishSetReadyCommand,
            ShowAvailableUnitsTable,
            ShowUnitInfo,
            null,
            isDefaultPlayer
                ? OnDefaultPlayerNameChanged
                : null,
            isDefaultPlayer,
            () => IsConnected);
    }

    // Implementation of abstract property from base class
    // Allow adding default player before connection, but require connection for additional players
    public override bool CanAddPlayer => (IsConnected || _players.Count == 0) && _players.Count < 4;

    // Implementation of abstract property from base class
    public override bool CanPublishCommands => IsConnected;
}
