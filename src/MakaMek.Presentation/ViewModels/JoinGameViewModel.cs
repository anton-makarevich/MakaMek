using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class JoinGameViewModel : NewGameViewModel
{
    private readonly IMechFactory _mechFactory;
    private readonly ITransportFactory _transportFactory;
    private readonly IBattleMapFactory _mapFactory;
    private string _serverIp = string.Empty;
    private bool _isConnected; // Track connection status

    public JoinGameViewModel(
        IRulesProvider rulesProvider,
        IMechFactory mechFactory,
        IUnitsLoader unitsLoader,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        ITransportFactory transportFactory,
        IBattleMapFactory mapFactory,
        IFileCachingService cachingService)
        : base(rulesProvider, unitsLoader, commandPublisher, toHitCalculator,
            pilotingSkillCalculator,
            consciousnessCalculator,
            heatEffectsCalculator,
            dispatcherService, gameFactory, cachingService)
    {
        _mechFactory = mechFactory;
        _transportFactory = transportFactory;
        _mapFactory = mapFactory;

        AddPlayerCommand = new AsyncCommand(() => AddPlayer());
        ConnectCommand = new AsyncCommand(ConnectToServer, (_)=>CanConnect);
    }

    // Implementation of the abstract method from base class
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
                     var newRemotePlayer = new Player(joinCmd.PlayerId, joinCmd.PlayerName, joinCmd.PlayerId.ToString()); // Use PlayerId as tilt for now
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
                // Handle navigation to BattleMapViewModel when battle map is set
                
                // Get the BattleMapViewModel and set the game
                var battleMapViewModel = NavigationService.GetViewModel<BattleMapViewModel>();
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
            _commandPublisher.Subscribe(HandleServerCommand);
            _localGame ??= _gameFactory.CreateClientGame(
                _rulesProvider,
                _mechFactory,
                _commandPublisher,
                _toHitCalculator,
                _pilotingSkillCalculator,
                _consciousnessCalculator,
                _heatEffectsCalculator,
                _mapFactory);
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
            Console.WriteLine($"Error connecting to server: {ex.Message}");
            IsConnected = false;
        }
    }

    // Implementation of template method from base class
    protected override PlayerViewModel CreatePlayerViewModel(Player player, bool isDefaultPlayer = false)
    {
        return new PlayerViewModel(
            player,
            isLocalPlayer: true,
            PublishJoinCommand,
            PublishSetReadyCommand,
            ShowTable,
            null,
            isDefaultPlayer 
                ? OnDefaultPlayerNameChanged
                : null);
    }

    // Implementation of abstract property from base class
    public override bool CanAddPlayer => IsConnected && _players.Count < 4;
    
    // Implementation of abstract property from base class
    public override bool CanPublishCommands => IsConnected; 
}
