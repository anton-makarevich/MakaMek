using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels.Wrappers;

namespace Sanet.MakaMek.Core.ViewModels;

public class JoinGameViewModel : NewGameViewModel
{
    private readonly ITransportFactory _transportFactory;
    private string _serverIp = string.Empty;
    private bool _isConnected; // Track connection status

    public JoinGameViewModel(
        IRulesProvider rulesProvider,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        ITransportFactory transportFactory)
        : base(rulesProvider, commandPublisher, toHitCalculator, dispatcherService, gameFactory)
    {
        _transportFactory = transportFactory;

        AddPlayerCommand = new AsyncCommand(AddPlayer);
        ConnectCommand = new AsyncCommand(ConnectToServer, (_)=>CanConnect);
    }

    // Call this method when the ViewModel becomes active
    public void InitializeClient()
    {
        _localGame ??= _gameFactory.CreateClientGame(
            _rulesProvider,
            _commandPublisher,
            _toHitCalculator);
    }

    // Implementation of the abstract method from base class
    protected override void HandleCommandInternal(IGameCommand command)
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
                        _ => {}); // No callback for ready state
                     
                     _players.Add(remotePlayerViewModel);
                }
                else if (existingPlayerVm.IsLocalPlayer)
                {
                     // Handle join confirmation for local player
                     existingPlayerVm.Player.Status = PlayerStatus.Joined;
                     existingPlayerVm.RefreshStatus();
                }
                break;
        }
        
        // Refresh bindings
        NotifyPropertyChanged(nameof(Players));
    }
    
    public string ServerIp
    {
        get => _serverIp;
        set
        {
            SetProperty(ref _serverIp, value);
            (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(CanConnect));
            NotifyPropertyChanged(nameof(ServerAddress));
            NotifyPropertyChanged(nameof(CanAddPlayer));
        }
    }
    
    public string ServerAddress => $"http://{ServerIp}:2439/makamekhub";
    
    public bool IsConnected
    {
        get => _isConnected;
        private set => SetProperty(ref _isConnected, value); // Update UI based on connection status
    }

    public ICommand ConnectCommand { get; private set; }

    public bool CanConnect => !string.IsNullOrWhiteSpace(ServerIp) && !IsConnected;

    private async Task ConnectToServer()
    {
        if (!CanConnect) return;

        try
        {
            Console.WriteLine($"Attempting to connect to {ServerIp}...");
            
            // Get access to the adapter from the command publisher
            var adapter = _commandPublisher.Adapter;
            
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

    // This method is now in the base class

    // This property is now in the base class

    

    // These methods are now in the base class with CanPublishCommands abstraction

    // Implementation of template method from base class
    protected override PlayerViewModel CreatePlayerViewModel(Player player)
    {
        return new PlayerViewModel(
            player,
            isLocalPlayer: true,
            _availableUnits,
            PublishJoinCommand,
            PublishSetReadyCommand);
    }

    // This property is now in the base class

    // This method is now in the base class

    // Implementation of abstract property from base class
    public override bool CanAddPlayer => IsConnected && _players.Count < 4;
    
    // Implementation of abstract property from base class
    public override bool CanPublishCommands => IsConnected; 
}
