using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public abstract class NewGameViewModel : BaseViewModel
{
    protected readonly ObservableCollection<PlayerViewModel> _players = [];
    private IEnumerable<UnitData> _availableUnits = [];

    protected readonly IRulesProvider _rulesProvider;
    private readonly IUnitsLoader _unitsLoader;
    protected readonly ICommandPublisher _commandPublisher;
    protected readonly IToHitCalculator _toHitCalculator;
    protected readonly IPilotingSkillCalculator _pilotingSkillCalculator;
    protected readonly IConsciousnessCalculator _consciousnessCalculator;
    protected readonly IHeatEffectsCalculator _heatEffectsCalculator;
    private readonly IDispatcherService _dispatcherService;
    protected readonly IGameFactory _gameFactory;
    private readonly IFileCachingService _cachingService;

    protected ClientGame? _localGame;

    public ICommand? AddPlayerCommand { get; protected set; }
    public ICommand RemovePlayerCommand { get; }

    private const string DefaultPlayerCacheKey = "DefaultPlayer";

    private PlayerViewModel? _activePlayer;

    protected NewGameViewModel(IRulesProvider rulesProvider,
        IUnitsLoader unitsLoader,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IPilotingSkillCalculator pilotingSkillCalculator,
        IConsciousnessCalculator consciousnessCalculator,
        IHeatEffectsCalculator heatEffectsCalculator,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory,
        IFileCachingService cachingService)
    {
        _rulesProvider = rulesProvider;
        _unitsLoader = unitsLoader;
        _commandPublisher = commandPublisher;
        _toHitCalculator = toHitCalculator;
        _pilotingSkillCalculator = pilotingSkillCalculator;
        _consciousnessCalculator = consciousnessCalculator;
        _heatEffectsCalculator = heatEffectsCalculator;
        _dispatcherService = dispatcherService;
        _gameFactory = gameFactory;
        _cachingService = cachingService;

        HideTableCommand = new AsyncCommand(HideTable);
        AddUnitCommand = new AsyncCommand(() => AddUnit(_activePlayer));
        RemovePlayerCommand = new AsyncCommand<PlayerViewModel?>(RemovePlayer);
    }

    // Common command handlers with template method pattern
    internal void HandleServerCommand(IGameCommand command)
    {
        _dispatcherService.RunOnUIThread(async () =>
        {
            await HandleCommandInternal(command);
        });
    }

    // Template method to be implemented by derived classes
    protected abstract Task HandleCommandInternal(IGameCommand command);

    // Common player management
    protected void PublishJoinCommand(PlayerViewModel playerVm)
    {
        if (!playerVm.IsLocalPlayer || !CanPublishCommands || _localGame == null) return;
        
        // Create pilot assignments for each unit
        var pilotAssignments = playerVm.Units.Select(unit => new PilotAssignmentData
        {
            UnitId = unit.Id ?? Guid.NewGuid(),
            PilotData = playerVm.GetPilotDataForUnit(unit.Id ?? Guid.NewGuid())?? PilotData.CreateDefaultPilot("MechWarrior","")
        }).ToList();

        _localGame.JoinGameWithUnits(playerVm.Player, playerVm.Units.ToList(), pilotAssignments);
    }

    protected void PublishSetReadyCommand(PlayerViewModel playerVm)
    {
        if (!playerVm.IsLocalPlayer || !CanPublishCommands || _localGame == null) return;
        
        var readyCommand = new UpdatePlayerStatusCommand
        {
            GameOriginId = Guid.NewGuid(),
            PlayerId = playerVm.Player.Id,
            PlayerStatus = PlayerStatus.Ready
        };
        _localGame.SetPlayerReady(readyCommand);
    }

    // Common properties
    public ObservableCollection<PlayerViewModel> Players => _players;
    
    internal ClientGame? LocalGame => _localGame;

    // Abstract/virtual properties to be implemented by derived classes
    public abstract bool CanPublishCommands { get; }
    public abstract bool CanAddPlayer { get; }

    // Common utility methods
    private string GetNextTint()
    {
        // Generate a vibrant, colorful random color
        return GenerateVibrantColor();
    }

    private string GenerateVibrantColor()
    {
        // Use HSV color space to ensure vibrant, colorful colors
        // Hue: 0-360 (full range for variety)
        // Saturation: 0.6-1.0 (avoid grayish colors)
        // Value/Brightness: 0.4-1.0 (avoid very dark colors but allow some darker ones for variety)
        var random = Random.Shared;

        var hue = random.NextDouble() * 360.0;
        var saturation = 0.6 + (random.NextDouble() * 0.4); // 0.6-1.0
        var value = 0.4 + (random.NextDouble() * 0.6); // 0.4-1.0

        // Convert HSV to RGB
        var rgb = HsvToRgb(hue, saturation, value);

        // Convert RGB to hex
        return $"#{rgb.Red:X2}{rgb.Green:X2}{rgb.Blue:X2}";
    }

    private static (int Red, int Green, int Blue) HsvToRgb(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
        var m = value - c;

        var rgbValues = hue switch
        {
            >= 0 and < 60 => (c, x, 0.0),
            >= 60 and < 120 => (x, c, 0.0),
            >= 120 and < 180 => (0.0, c, x),
            >= 180 and < 240 => (0.0, x, c),
            >= 240 and < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return (
            (int)Math.Round((rgbValues.Item1 + m) * 255),
            (int)Math.Round((rgbValues.Item2 + m) * 255),
            (int)Math.Round((rgbValues.Item3 + m) * 255)
        );
    }

    // Common player creation logic with template method pattern
    protected virtual Task AddPlayer(PlayerData? playerData = null)
    {
        if (!CanAddPlayer) return Task.CompletedTask;
        var isDefaultPlayer = playerData != null;

        // Generate random 4-digit number for player name
        playerData ??= PlayerData.CreateDefault() with { Tint = GetNextTint() };

        // Create Local Player Object
        var newPlayer = new Player(playerData.Value, Guid.NewGuid());

        // Create Local ViewModel Wrapper with customizable callbacks
        var playerViewModel = CreatePlayerViewModel(newPlayer, isDefaultPlayer);

        // Add to Local UI Collection
        _players.Add(playerViewModel);
        NotifyPropertyChanged(nameof(CanAddPlayer));

        return Task.CompletedTask;
    }

    // Template method for creating player view models with appropriate callbacks
    protected abstract PlayerViewModel CreatePlayerViewModel(Player player, bool isDefaultPlayer = false);

    /// <summary>
    /// Removes a player from the game setup
    /// </summary>
    /// <param name="playerVm">The player to remove</param>
    /// <returns>Task representing the async operation</returns>
    private Task RemovePlayer(PlayerViewModel? playerVm)
    {
        if (!CanRemovePlayer(playerVm)) return Task.CompletedTask;

        _players.Remove(playerVm!);
        NotifyPropertyChanged(nameof(CanAddPlayer));
        return ReferenceEquals(_activePlayer, playerVm) 
            ? HideTable() 
            : Task.CompletedTask;
    }
    
    private static bool CanRemovePlayer(PlayerViewModel? playerVm)
    {
        return playerVm?.IsRemovable ?? false;
    }

    /// <summary>
    /// Loads or creates the default player from cache
    /// </summary>
    private async Task<PlayerData> LoadOrCreateDefaultPlayer()
    {
        try
        {
            var cachedData = await _cachingService.TryGetCachedFile(DefaultPlayerCacheKey);
            if (cachedData != null)
            {
                var json = System.Text.Encoding.UTF8.GetString(cachedData);
                return JsonSerializer.Deserialize<PlayerData>(json);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading default player from cache: {ex.Message}");
        }

        // Create new default player if cache load failed
        var defaultPlayerData = PlayerData.CreateDefault() with { Tint = GetNextTint() };
        await SaveDefaultPlayer(defaultPlayerData);
        return defaultPlayerData;
    }

    /// <summary>
    /// Saves the default player to cache
    /// </summary>
    private async Task SaveDefaultPlayer(PlayerData playerData)
    {
        try
        {
            var json = JsonSerializer.Serialize(playerData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await _cachingService.SaveToCache(DefaultPlayerCacheKey, bytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving default player to cache: {ex.Message}");
        }
    }

    public override void AttachHandlers()
    {
        base.AttachHandlers();
        _ = LoadAvailableUnits();

        // Auto-add default player
        _ = AddDefaultPlayer();
    }
    
    private async Task LoadAvailableUnits()
    {
        _availableUnits = await _unitsLoader.LoadUnits();
        
        // Initialize the AvailableUnitsTableViewModel
        AvailableUnitsTableViewModel = new AvailableUnitsTableViewModel(
            AvailableUnits,
            AddUnitCommand);
    }

    /// <summary>
    /// Adds the default player automatically when the view loads
    /// </summary>
    private async Task AddDefaultPlayer()
    {
        if (!CanAddPlayer) return;

        // Load or create default player
        var defaultPlayer = await LoadOrCreateDefaultPlayer();

        await AddPlayer(defaultPlayer);
    }

    /// <summary>
    /// Handles player name changes for the default player
    /// </summary>
    protected async Task OnDefaultPlayerNameChanged(Player updatedPlayer)
    {
        // Save the updated player to cache
        await SaveDefaultPlayer(updatedPlayer.ToData());
    }

    public List<UnitData> AvailableUnits => _availableUnits.ToList();
    
        
    public ICommand HideTableCommand { get; }
    private ICommand AddUnitCommand { get; }
    
    protected void ShowTable(PlayerViewModel playerVm)
    {
        _activePlayer = playerVm;
        IsTableVisible = true;
    }

    private Task HideTable()
    {
        _activePlayer = null;
        // Hide the table
        IsTableVisible = false;
        return Task.CompletedTask;
    }
    
    private Task AddUnit(PlayerViewModel? playerVm)
    {
        if (playerVm == null) return Task.CompletedTask;
        // Get the selected unit from the table ViewModel
        var selectedUnit = AvailableUnitsTableViewModel?.SelectedUnit;
        if (!selectedUnit.HasValue) return Task.CompletedTask;

        var unit = selectedUnit.Value;
        playerVm.AddUnit(unit);
        return HideTable();
    }
    
    private bool _isTableVisible;
    public bool IsTableVisible
    {
        get => _isTableVisible;
        set => SetProperty(ref _isTableVisible, value);
    }
    
    /// <summary>
    /// Gets the ViewModel for the available units table
    /// </summary>
    public AvailableUnitsTableViewModel? AvailableUnitsTableViewModel { get; private set; }
}
