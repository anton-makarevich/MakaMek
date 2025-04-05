using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Factory;
using Sanet.MakaMek.Core.Services;

namespace Sanet.MakaMek.Core.ViewModels;

public class StartNewGameViewModel : BaseViewModel
{
    private int _mapWidth = 15;
    private int _mapHeight = 17;
    private int _forestCoverage = 20;
    private int _lightWoodsPercentage = 30;

    private readonly ObservableCollection<PlayerViewModel> _players = [];
    private IEnumerable<UnitData> _availableUnits = [];

    private readonly IGameManager _gameManager;
    private readonly IRulesProvider _rulesProvider;
    private readonly ICommandPublisher _commandPublisher;
    private readonly IToHitCalculator _toHitCalculator;
    private readonly IDispatcherService _dispatcherService; 
    private readonly IGameFactory _gameFactory; 
    
    private ClientGame _localGame;

    public StartNewGameViewModel(
        IGameManager gameManager, 
        IRulesProvider rulesProvider, 
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory) // Added factory parameter
    {
        _gameManager = gameManager;
        _rulesProvider = rulesProvider;
        _commandPublisher = commandPublisher;
        _toHitCalculator = toHitCalculator;
        _dispatcherService = dispatcherService; 
        _gameFactory = gameFactory; 
    }

    public async Task InitializeLobbyAndSubscribe()
    {
        await _gameManager.InitializeLobby();
        _commandPublisher.Subscribe(HandleServerCommand);
        // Use the factory to create the ClientGame
        _localGame = _gameFactory.CreateClientGame(
            _rulesProvider,
            _commandPublisher, _toHitCalculator);
        // Update server IP initially if needed
        NotifyPropertyChanged(nameof(ServerIpAddress));
    }

    // Handle commands coming FROM the server/other clients
    internal void HandleServerCommand(IGameCommand command)
    {
        // Ensure UI updates happen on the correct thread
        _dispatcherService.RunOnUIThread(() =>
        {
            switch (command)
            {
                // Handle player joining (potentially echo of local or a remote player)
                case JoinGameCommand joinCmd:
                    var existingPlayerVm = _players.FirstOrDefault(p => p.Player.Id == joinCmd.PlayerId);
                    if (existingPlayerVm != null)
                    {
                        // Player exists - likely the echo for a local player who just clicked Join
                        if (existingPlayerVm.IsLocalPlayer)
                        {
                            //TODO: mar as joined
                        }
                        // Else: Remote player sending join again? Ignore or log warning.
                    }
                    else
                    {   // Player doesn't exist - must be a remote player joining
                        var remotePlayer = new Player(joinCmd.PlayerId, joinCmd.PlayerName, joinCmd.Tint);
                        var remotePlayerVm = new PlayerViewModel(
                            remotePlayer, 
                            isLocalPlayer: false, // Mark as remote
                            [], 
                            null, // No join action needed for remote
                            () => NotifyPropertyChanged(nameof(CanStartGame))); 
                        
                        remotePlayerVm.AddUnits(joinCmd.Units); // Add units received from command
                        _players.Add(remotePlayerVm);
                        NotifyPropertyChanged(nameof(CanAddPlayer));
                        NotifyPropertyChanged(nameof(CanStartGame));
                    }
                    break;
                    
                // Handle player status updates (e.g., Ready state)
                case UpdatePlayerStatusCommand statusCmd:
                    var playerToUpdate = _players.FirstOrDefault(p => p.Player.Id == statusCmd.PlayerId);
                    if (playerToUpdate != null)
                    {
                        // Assuming PlayerViewModel has a way to update status, or direct access to Player.Status
                        // This might require PlayerViewModel changes if Player property is not directly settable/updatable
                        playerToUpdate.Player.Status = statusCmd.PlayerStatus; 
                        // We might need a more robust way to trigger UI updates in PlayerViewModel if Status change doesn't auto-notify
                        NotifyPropertyChanged(nameof(CanStartGame)); // Re-evaluate if game can start
                    }
                    break;
            }
        });
    }


    public string MapWidthLabel => "Map Width";
    public string MapHeightLabel => "Map Height";
    public string ForestCoverageLabel => "Forest Coverage";
    public string LightWoodsLabel => "Light Woods Percentage";

    public int MapWidth
    {
        get => _mapWidth;
        set => SetProperty(ref _mapWidth, value);
    }

    public int MapHeight
    {
        get => _mapHeight;
        set => SetProperty(ref _mapHeight, value);
    }

    public int ForestCoverage
    {
        get => _forestCoverage;
        set
        {
            SetProperty(ref _forestCoverage, value);
            NotifyPropertyChanged(nameof(IsLightWoodsEnabled));
        }
    }

    public int LightWoodsPercentage
    {
        get => _lightWoodsPercentage;
        set => SetProperty(ref _lightWoodsPercentage, value);
    }

    public bool IsLightWoodsEnabled => _forestCoverage > 0;

    public bool CanStartGame => Players.Count > 0 && Players.All(p => p.Units.Count > 0); //&& p.Player.Status == PlayerStatus.Playing);
    
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
        // 1. Generate Map
        var map = ForestCoverage == 0
            ? BattleMap.GenerateMap(MapWidth, MapHeight, new SingleTerrainGenerator(
                MapWidth, MapHeight, new ClearTerrain()))
            : BattleMap.GenerateMap(MapWidth, MapHeight, new ForestPatchesGenerator(
                MapWidth, MapHeight,
                forestCoverage: ForestCoverage / 100.0,
                lightWoodsProbability: LightWoodsPercentage / 100.0));
        
        var hexDataList = map.GetHexes().Select(hex => hex.ToData()).ToList();
        var localBattleMap = BattleMap.CreateFromData(hexDataList);
        
        // 2. Set BattleMap on GameManager/ServerGame
        _gameManager.SetBattleMap(map);

        // 3. Host Client for local player(s) 
        _localGame.SetBattleMap(localBattleMap);

        var battleMapViewModel = NavigationService.GetViewModel<BattleMapViewModel>();
        battleMapViewModel.Game = _localGame;
        
        // Navigate to BattleMap view
        await NavigationService.NavigateToViewModelAsync(battleMapViewModel);
    });

    public void InitializeUnits(List<UnitData> units)
    {
        // Logic to load available units for selection
        _availableUnits =units;
    }

    public ObservableCollection<PlayerViewModel> Players => _players;

    public ICommand AddPlayerCommand => new AsyncCommand(AddPlayer);

    // Method to be passed to local PlayerViewModel instances
    private void PublishJoinCommand(PlayerViewModel playerVm)
    {
        if (!playerVm.IsLocalPlayer) return; // Should only be called for local players
        _localGame.JoinGameWithUnits(playerVm.Player, playerVm.Units.ToList());
    }
    
    private Task AddPlayer()
    {
        if (!CanAddPlayer) return Task.CompletedTask; 
 
        // 1. Create Local Player Object
        var newPlayer = new Player(Guid.NewGuid(), $"Player {_players.Count + 1}", GetNextTilt());
        
        // 2. Create Local ViewModel Wrapper
        var playerViewModel = new PlayerViewModel(
            newPlayer,
            isLocalPlayer: true, // Mark as local
            _availableUnits,
            PublishJoinCommand, // Pass the action to publish the Join command
            () => NotifyPropertyChanged(nameof(CanStartGame)));
        
        // 3. Add to Local UI Collection
        _players.Add(playerViewModel);
        NotifyPropertyChanged(nameof(CanAddPlayer));
        NotifyPropertyChanged(nameof(CanStartGame)); // CanStartGame might be false until units are added
        
        return Task.CompletedTask;
    }

    private string GetNextTilt()
    {
        // Simple color cycling based on player count
        return Players.Count switch
        {
            0 => "#FFFFFF", // White
            1 => "#FF0000", // Red
            2 => "#0000FF", // Blue
            3 => "#FFFF00", // Yellow
            _ => "#FFFFFF"
        };
    }
    
    public bool CanAddPlayer => _players.Count < 4; // Limit to 4 players for now

    public void Dispose()
    {
        // Dispose game manager if this ViewModel owns it (depends on DI lifetime)
        _gameManager.Dispose(); 
        GC.SuppressFinalize(this);
    }
}