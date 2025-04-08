using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.ViewModels;

public class JoinGameViewModel : BaseViewModel
{
    private readonly ObservableCollection<PlayerViewModel> _players = [];
    private IEnumerable<UnitData> _availableUnits = []; // Assuming units might be later defined by server

    private readonly IRulesProvider _rulesProvider;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly IDispatcherService _dispatcherService;
    private readonly IGameFactory _gameFactory;
    private readonly ITransportFactory _transportFactory;

    private ClientGame? _localGame;
    private string _serverAddress = string.Empty;
    private bool _isConnected; // Track connection status

    public JoinGameViewModel(
        IRulesProvider rulesProvider,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        ITransportFactory transportFactory)
    {
        _rulesProvider = rulesProvider;
        _commandPublisher = commandPublisher;
        _toHitCalculator = toHitCalculator;
        _dispatcherService = dispatcherService;
        _gameFactory = gameFactory;
        _transportFactory = transportFactory;

        AddPlayerCommand = new AsyncCommand(AddPlayer);
        ConnectCommand = new AsyncCommand(ConnectToServer, (_)=>CanConnect);
    }

    // Call this method when the ViewModel becomes active
    public void InitializeClientAsync()
    {
        _localGame ??= _gameFactory.CreateClientGame(
            _rulesProvider,
            _commandPublisher,
            _toHitCalculator);
    }

    // Handle commands coming FROM the server AFTER connection
    internal void HandleServerCommand(IGameCommand command)
    {
        // Ensure UI updates happen on the correct thread
        _dispatcherService.RunOnUIThread(() =>
        {
            // Placeholder: Similar logic to StartNewGameViewModel.HandleServerCommand
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
                         var newRemotePlayer = new Player(joinCmd.PlayerId, joinCmd.PlayerName, joinCmd.PlayerId.ToString()); // Use PlayerId as tilt for now
                         var remotePlayerViewModel = new PlayerViewModel(
                            newRemotePlayer,
                            isLocalPlayer: false,
                            _availableUnits,
                            _ => {}, // Remote players don't publish join
                            _ => {}, // Remote players don't publish ready
                            () => {}); 
                         _players.Add(remotePlayerViewModel);
                         NotifyPropertyChanged(nameof(CanAddPlayer));
                    }
                    else if (existingPlayerVm.IsLocalPlayer)
                    {
                         // Handle join confirmation for local player
                         existingPlayerVm.Player.Status = PlayerStatus.Joined;
                         existingPlayerVm.RefreshStatus();
                    }
                   
                    break;
                
                 // Add handlers for PlayerRemovedCommand, GameStartedCommand etc.
            }
            NotifyPropertyChanged(nameof(Players)); // Refresh the list binding
        });
    }
    
    public string ServerAddress
    {
        get => _serverAddress;
        set
        {
            SetProperty(ref _serverAddress, value);
            (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }
    
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value); // Update UI based on connection status
    }

    public ICommand ConnectCommand { get; }

    public bool CanConnect=> !string.IsNullOrWhiteSpace(ServerAddress) && !IsConnected;

    private async Task ConnectToServer()
    {
        if (!CanConnect) return;

        try
        {
            Console.WriteLine($"Attempting to connect to {ServerAddress}...");
            
            // Get access to the adapter from the command publisher
            var adapter = (_commandPublisher as CommandPublisher)?.Adapter;
            if (adapter == null)
            {
                throw new InvalidOperationException("Command publisher adapter not available");
            }
            
            // Clear any existing publishers and prepare for new connection
            adapter.ClearPublishers();
            
            // Create network client publisher using the factory and connect
            var client = await _transportFactory.CreateAndStartClientPublisher(ServerAddress);
            adapter.AddPublisher(client);
            
            IsConnected = true;
            (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged(); // Disable connect button
            NotifyPropertyChanged(nameof(CanAddPlayer)); // Enable Add Player once connected
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to server: {ex.Message}");
            IsConnected = false;
        }
    }

    public void InitializeUnits(List<UnitData> units)
    {
        // Logic to load available units for selection (might come from server later)
        _availableUnits = units;
    }

    public ObservableCollection<PlayerViewModel> Players => _players;

    public ICommand AddPlayerCommand { get; }

    // Method to be passed to local PlayerViewModel instances
    private void PublishJoinCommand(PlayerViewModel playerVm)
    {
        if (!playerVm.IsLocalPlayer || !IsConnected || _localGame == null) return;
        // This sends the Join command *after* the initial connection is established
        _localGame.JoinGameWithUnits(playerVm.Player, playerVm.Units.ToList());
    }

    // Method to be passed to local PlayerViewModel instances to set player as ready
    private void PublishSetReadyCommand(PlayerViewModel playerVm)
    {
        if (!playerVm.IsLocalPlayer || !IsConnected || _localGame == null) return;
        
        var readyCommand = new UpdatePlayerStatusCommand
        {
            PlayerId = playerVm.Player.Id,
            PlayerStatus = PlayerStatus.Ready
        };
        _localGame.SetPlayerReady(readyCommand);
    }

    private Task AddPlayer()
    {
        if (!CanAddPlayer) return Task.CompletedTask;

        // 1. Create Local Player Object
        var newPlayer = new Player(Guid.NewGuid(), $"Player {_players.Count(p=>p.IsLocalPlayer) + 1}", GetNextTilt());

        // 2. Create Local ViewModel Wrapper
        var playerViewModel = new PlayerViewModel(
            newPlayer,
            isLocalPlayer: true,
            _availableUnits,
            PublishJoinCommand,
            PublishSetReadyCommand);

        // 3. Add to Local UI Collection
        _players.Add(playerViewModel);
        NotifyPropertyChanged(nameof(CanAddPlayer));

        return Task.CompletedTask;
    }

    internal ClientGame? LocalGame => _localGame;

    private string GetNextTilt()
    {
        // Simple color cycling based on local player count
        return _players.Count(p=>p.IsLocalPlayer) switch
        {
            0 => "#FFFFFF", // White
            1 => "#FF0000", // Red
            2 => "#0000FF", // Blue
            3 => "#FFFF00", // Yellow
            _ => "#FFFFFF"
        };
    }

    // Can add player only if connected and less than max players (e.g., 4)
    public bool CanAddPlayer => IsConnected && _players.Count < 4; 

}
