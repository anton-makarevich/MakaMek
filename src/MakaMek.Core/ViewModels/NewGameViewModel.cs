using System.Collections.ObjectModel;
using System.Windows.Input;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Combat;
using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Client;
using Sanet.MakaMek.Core.Models.Game.Factories;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Core.Utils.TechRules;
using Sanet.MakaMek.Core.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Core.ViewModels;

public abstract class NewGameViewModel : BaseViewModel
{
    protected readonly ObservableCollection<PlayerViewModel> _players = [];
    protected IEnumerable<UnitData> _availableUnits = [];

    protected readonly IRulesProvider _rulesProvider;
    private readonly IUnitsLoader _unitsLoader;
    protected readonly ICommandPublisher _commandPublisher;
    protected readonly IToHitCalculator _toHitCalculator;
    private readonly IDispatcherService _dispatcherService;
    protected readonly IGameFactory _gameFactory;
    
    protected ClientGame? _localGame;
    
    public ICommand AddPlayerCommand { get; protected set; }

    protected NewGameViewModel(
        IRulesProvider rulesProvider,
        IUnitsLoader unitsLoader,
        ICommandPublisher commandPublisher,
        IToHitCalculator toHitCalculator,
        IDispatcherService dispatcherService,
        IGameFactory gameFactory)
    {
        _rulesProvider = rulesProvider;
        _unitsLoader = unitsLoader;
        _commandPublisher = commandPublisher;
        _toHitCalculator = toHitCalculator;
        _dispatcherService = dispatcherService;
        _gameFactory = gameFactory;
    }

    // Common command handlers with template method pattern
    internal void HandleServerCommand(IGameCommand command)
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            HandleCommandInternal(command);
        });
    }

    // Template method to be implemented by derived classes
    protected abstract void HandleCommandInternal(IGameCommand command);

    // Common player management
    protected void PublishJoinCommand(PlayerViewModel playerVm)
    {
        if (!playerVm.IsLocalPlayer || !CanPublishCommands || _localGame == null) return;
        _localGame.JoinGameWithUnits(playerVm.Player, playerVm.Units.ToList());
    }

    protected void PublishSetReadyCommand(PlayerViewModel playerVm)
    {
        if (!playerVm.IsLocalPlayer || !CanPublishCommands || _localGame == null) return;
        
        var readyCommand = new UpdatePlayerStatusCommand
        {
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
    protected string GetNextTilt()
    {
        // Simple color cycling based on player count
        return _players.Count(p => p.IsLocalPlayer) switch
        {
            0 => "#FFFFFF", // White
            1 => "#FF0000", // Red
            2 => "#0000FF", // Blue
            3 => "#FFFF00", // Yellow
            _ => "#FFFFFF"
        };
    }

    // Common player creation logic with template method pattern
    protected virtual Task AddPlayer()
    {
        if (!CanAddPlayer) return Task.CompletedTask;

        // Create Local Player Object
        var newPlayer = new Player(Guid.NewGuid(), $"Player {_players.Count(p => p.IsLocalPlayer) + 1}", GetNextTilt());
        
        // Create Local ViewModel Wrapper with customizable callbacks
        var playerViewModel = CreatePlayerViewModel(newPlayer);
        
        // Add to Local UI Collection
        _players.Add(playerViewModel);
        NotifyPropertyChanged(nameof(CanAddPlayer));
        
        return Task.CompletedTask;
    }

    // Template method for creating player view models with appropriate callbacks
    protected abstract PlayerViewModel CreatePlayerViewModel(Player player);

    public override async void AttachHandlers()
    {
        base.AttachHandlers();
        _availableUnits = await _unitsLoader.LoadUnits();
    }
    
    public List<UnitData> AvailableUnits => _availableUnits.ToList();
}
