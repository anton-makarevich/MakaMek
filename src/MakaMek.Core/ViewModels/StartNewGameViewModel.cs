using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels.Wrappers;

namespace Sanet.MakaMek.Core.ViewModels;

public class StartNewGameViewModel : NewGameViewModel, IDisposable
{
    private int _mapWidth = 15;
    private int _mapHeight = 17;
    private int _forestCoverage = 20;
    private int _lightWoodsPercentage = 30;

    private readonly IGameManager _gameManager;
    
    public StartNewGameViewModel(
        IGameManager gameManager, 
        IUnitsLoader unitsLoader,
        IRulesProvider rulesProvider, 
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory)
        : base(rulesProvider, unitsLoader, commandPublisher, toHitCalculator, dispatcherService, gameFactory)
    {
        _gameManager = gameManager;
        AddPlayerCommand = new AsyncCommand(AddPlayer);
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

    // Implementation of the abstract method from base class
    protected override void HandleCommandInternal(IGameCommand command)
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
                    var remotePlayer = new Player(joinCmd.PlayerId, joinCmd.PlayerName, joinCmd.Tint);
                    var remotePlayerVm = new PlayerViewModel(
                        remotePlayer, 
                        isLocalPlayer: false, // Mark as remote
                        _availableUnits, 
                        _ => {}, // No join action needed for remote
                        _ => {}, // No set ready action needed for remote
                        () => NotifyPropertyChanged(nameof(CanStartGame))); 
                    
                    remotePlayerVm.AddUnits(joinCmd.Units); // Add units received from command
                    _players.Add(remotePlayerVm);
                    NotifyPropertyChanged(nameof(CanAddPlayer));
                    NotifyPropertyChanged(nameof(CanStartGame));
                }
                break;
        }
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
        _localGame?.SetBattleMap(localBattleMap);

        var battleMapViewModel = NavigationService.GetViewModel<BattleMapViewModel>();
        battleMapViewModel.Game = _localGame;
        
        // Navigate to BattleMap view
        await NavigationService.NavigateToViewModelAsync(battleMapViewModel);
    });
    
    // Implementation of template method from base class
    protected override PlayerViewModel CreatePlayerViewModel(Player player)
    {
        return new PlayerViewModel(
            player,
            isLocalPlayer: true,
            _availableUnits,
            PublishJoinCommand,
            PublishSetReadyCommand,
            () => NotifyPropertyChanged(nameof(CanStartGame)));
    }
    
    // Override the base AddPlayer to add additional notification
    protected override Task AddPlayer()
    {
        var result = base.AddPlayer();
        NotifyPropertyChanged(nameof(CanStartGame)); // CanStartGame might be false until units are added
        return result;
    }
    
    // Implementation of abstract property from base class
    public override bool CanAddPlayer => _players.Count < 4; // Limit to 4 players for now
    
    // Implementation of abstract property from base class
    public override bool CanPublishCommands => true; // TODO: is it actually always true?

    public void Dispose()
    {
        // Dispose game manager if this ViewModel owns it (depends on DI lifetime)
        _gameManager.Dispose(); 
        GC.SuppressFinalize(this);
    }

    public override async void AttachHandlers()
    {
        base.AttachHandlers();
        await InitializeLobbyAndSubscribe();
    }
}