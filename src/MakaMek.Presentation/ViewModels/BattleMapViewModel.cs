using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Map.Services;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.Models;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// Border outline rendering data for a highlighted hex.
/// </summary>
/// <param name="EdgeMask">The 6-bit edge mask to draw.</param>
/// <param name="HighlightType">The highlight the outline belongs to; the renderer resolves its themed brush.</param>
/// <param name="Thickness">The outline stroke thickness.</param>
public sealed record HighlightBoundaryOutline(byte EdgeMask, IHexHighlightType HighlightType, double Thickness);

public class BattleMapViewModel : BaseViewModel, IDisposable
{
    private IClientGame? _game;
    private IDisposable? _gameSubscription;
    private IDisposable? _commandSubscription;
    private readonly IObservable<ConnectionStatus>? _connectionStatusSource;
    private readonly ObservableCollection<string> _commandLog = [];
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IPlatformService _platformService;
    private readonly IPdfExportService? _pdfExportService;
    private readonly IFileService? _fileService;
    private List<UiEventViewModel> _selectedUnitEvents = [];
    private readonly PropertyChangedEventHandler? _hexConfigurationChangedHandler;
    private readonly Dictionary<Guid, int> _initiativeRolls = [];
    private Guid? _announcedInitiativeWinnerId;

    /// <summary>
    /// The phase as of the command currently being processed, which is not the same thing as
    /// <see cref="IClientGame.TurnPhase"/>. Commands are delivered through ObserveOn, so a handler
    /// runs after the command arrived, and the server publishes a whole phase worth of commands in
    /// one synchronous burst: by the time a deferred handler runs, the live phase has often moved
    /// on. Anything in ProcessCommand that needs to know "which phase was this command part of"
    /// must read this, not the game.
    /// </summary>
    private PhaseNames? _commandStreamPhase;
    private IClientGame? _commandFeedbackGame;

    private IReadOnlyDictionary<HexCoordinates, HighlightBoundaryOutline> _highlightBoundaryOutlines =
        new Dictionary<HexCoordinates, HighlightBoundaryOutline>();


    public HexCoordinates? DirectionSelectorPosition
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsDirectionSelectorVisible
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public IEnumerable<HexDirection>? AvailableDirections
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public List<PathSegmentViewModel>? MovementPath
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public List<WeaponAttackViewModel>? WeaponAttacks
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Boundary outlines for the current highlighted coordinate group.
    /// </summary>
    public IReadOnlyDictionary<HexCoordinates, HighlightBoundaryOutline> HighlightBoundaryOutlines =>
        _highlightBoundaryOutlines;

    public AimedShotLocationSelectorViewModel? UnitPartSelector
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsUnitPartSelectorVisible
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public SurfaceSelectorViewModel? SurfaceSelector
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsSurfaceSelectorVisible
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public HexCoordinates? SurfaceSelectorPosition
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public void ShowSurfaceSelector(HexCoordinates position, SurfaceSelectorViewModel vm)
    {
        SurfaceSelectorPosition = position;
        SurfaceSelector = vm;
        IsSurfaceSelectorVisible = true;
        NotifyStateChanged();
    }

    public void HideSurfaceSelector()
    {
        IsSurfaceSelectorVisible = false;
        SurfaceSelector = null;
        SurfaceSelectorPosition = null;
    }

    public ICommand HideSurfaceSelectorCommand => new AsyncCommand(() =>
    {
        HideSurfaceSelector();
        return Task.CompletedTask;
    });

    public ICommand SurfaceSelectedCommand { get; }

    public ICommand DirectionSelectedCommand { get; }

    public BattleMapViewModel(
        IImageService imageService,
        ITerrainAssetService terrainAssetService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IRulesProvider rulesProvider,
        IPlatformService platformService,
        IPdfExportService? pdfExportService = null,
        IFileService? fileService = null,
        ITerrainBitmaskService? terrainBitmaskService = null,
        ICommandPublisher? commandPublisher = null,
        ILogger? connectionLogger = null)
    {
        ImageService = imageService;
        TerrainAssetService = terrainAssetService;
        TerrainBitmaskService = terrainBitmaskService;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;
        _platformService = platformService;
        _pdfExportService = pdfExportService;
        _fileService = fileService;
        CurrentState = new IdleState();
        HideBodyPartSelectorCommand = new AsyncCommand(() =>
        {
            HideAimedShotLocationSelector();
            return Task.CompletedTask;
        });
        HeatProjection = new HeatProjectionViewModel(_localizationService, rulesProvider);
        SelectedUnitHeatProjection = new HeatProjectionViewModel(_localizationService, rulesProvider);
        LeaveGameCommand = new AsyncCommand(LeaveGame);
        TurnNotificationShownCommand = new AsyncCommand<TurnNotification>(notification =>
        {
            if (notification != null)
                TurnNotifications.Remove(notification);
            return Task.CompletedTask;
        });
        SurfaceSelectedCommand = new AsyncCommand<HexSurface>(surface =>
        {
            SurfaceSelector?.SelectSurface(surface);
            return Task.CompletedTask;
        });
        DirectionSelectedCommand = new AsyncCommand<HexDirection>(direction =>
        {
            CurrentState.HandleFacingSelection(direction);
            return Task.CompletedTask;
        });
        HexConfiguration = new HexRenderConfigurationViewModel();
        _hexConfigurationChangedHandler = (_, _) => NotifyPropertyChanged(nameof(HexConfiguration));
        HexConfiguration.PropertyChanged += _hexConfigurationChangedHandler;
        ConnectionStatus = new ConnectionStatusViewModel(null, Scheduler, connectionLogger);
        _connectionStatusSource = commandPublisher?.Adapter.ConnectionStatusChanges;
        ConnectionStatus.PropertyChanged += OnConnectionStatusPropertyChanged;
    }

    /// <summary>
    /// Gets the child ViewModel that tracks connection status.
    /// </summary>
    public ConnectionStatusViewModel ConnectionStatus { get; }

    /// <summary>
    /// Gets whether the connection status banner should be shown on the battle map.
    /// </summary>
    public bool IsConnectionBannerVisible => ConnectionStatus.IsConnectionDegraded;

    private void OnConnectionStatusPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionStatusViewModel.IsConnectionDegraded))
        {
            NotifyPropertyChanged(nameof(IsConnectionBannerVisible));
        }
        else if (e.PropertyName == nameof(ConnectionStatusViewModel.OnlineConnectionStatus)
                 && ConnectionStatus.OnlineConnectionStatus == Core.Services.Transport.ConnectionStatus.Closed)
        {
            HandleTransportClosed();
        }
    }

    private void HandleTransportClosed()
    {
        if (Game == null || IsGameOver) return;
        IsGameOver = true;
        GameEndReason = GameEndReason.HostDisconnected;
        NotifyStateChanged();
        ShowTransportClosedDialogAsync()
            .SafeFireAndForget(ex => Game?.Logger.LogError(ex, "Error showing transport closed dialog"));
    }

    private async Task ShowTransportClosedDialogAsync()
    {
        var okAction = new UiAction
        {
            Title = _localizationService.GetString("Dialog_Ok")
        };

        var selectedAction = await NavigationService.AskForActionAsync(
            _localizationService.GetString("Dialog_GameInterrupted_Title"),
            _localizationService.GetString("Dialog_GameInterrupted_Message"),
            okAction);

        if (selectedAction != okAction) return;

        await GoToMainMenu();
    }

    private async Task LeaveGame()
    {
        // Show confirmation dialog
        var yesAction = new UiAction
        {
            Title = _localizationService.GetString("Dialog_Yes")
        };

        var noAction = new UiAction
        {
            Title = _localizationService.GetString("Dialog_No")
        };

        var selectedAction = await NavigationService.AskForActionAsync(
            _localizationService.GetString("Dialog_LeaveGame_Title"),
            _localizationService.GetString("Dialog_LeaveGame_Message"),
            yesAction,
            noAction);

        // If the user didn't select "Yes", cancel the operation
        if (selectedAction != yesAction)
        {
            return;
        }

        // Send PlayerLeftCommand for each local player
        if (Game != null)
        {
            foreach (var playerId in Game.LocalPlayers)
            {
                if (Game == null || Game.IsDisposed) return;
                Game.LeaveGame(playerId);
            }

            // Small delay to allow command to be sent
            await Task.Delay(100);
        }

        await GoToMainMenu();
    }

    public IClientGame? Game
    {
        get => _game;
        set
        {
            CommandFeedbackLabel = null;
            SetProperty(ref _game, value);
            SubscribeToGameChanges();
        }
    }

    public ILocalizationService LocalizationService => _localizationService;

    public IReadOnlyCollection<string> CommandLog => _commandLog;

    /// <summary>
    /// Gets the latest server rejection message for display without opening the command log.
    /// </summary>
    public string? CommandFeedbackLabel
    {
        get;
        private set
        {
            var changed = !string.Equals(field, value, StringComparison.Ordinal);
            SetProperty(ref field, value);
            if (changed)
                NotifyPropertyChanged(nameof(IsCommandFeedbackVisible));
        }
    }

    /// <summary>
    /// Gets whether a command rejection should be shown in the turn-status area.
    /// </summary>
    public bool IsCommandFeedbackVisible => !string.IsNullOrWhiteSpace(CommandFeedbackLabel);

    public bool IsGameOver
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public GameEndReason GameEndReason
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public ObservableCollection<WeaponSelectionViewModel> WeaponSelectionItems { get; } = [];

    public TargetSelectionViewModel? SelectedTarget
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(IsWeaponSelectionVisible));
        }
    }

    public HeatProjectionViewModel HeatProjection { get; }

    public HeatProjectionViewModel SelectedUnitHeatProjection { get; }

    public bool IsWeaponSelectionVisible
    {
        get => CurrentState is WeaponsAttackState { CurrentStep: WeaponsAttackStep.TargetSelection }
               && SelectedTarget != null
               && field;
        set => SetProperty(ref field, value);
    }

    public void CloseWeaponSelectionCommand()
    {
        IsWeaponSelectionVisible = false;
    }

    private void SubscribeToGameChanges()
    {
        _gameSubscription?.Dispose();
        _commandSubscription?.Dispose();
        UnsubscribeFromCommandFeedback();

        if (Game is null) return;

        _commandFeedbackGame = Game;
        _commandFeedbackGame.CommandTimedOut += OnCommandTimedOut;
        _commandFeedbackGame.CommandRejectedLocally += OnCommandRejectedLocally;

        _commandStreamPhase = Game.TurnPhase;
        _commandSubscription = Game.Commands
            .ObserveOn(_dispatcherService.Scheduler)
            .Subscribe(ProcessCommand);

        _gameSubscription = Game.TurnChanges
            .StartWith(Game.Turn)
            .CombineLatest(Game.PhaseChanges.StartWith(Game.TurnPhase),
                Game.PhaseStepChanges.StartWith(Game.PhaseStepState),
                (turn, phase, state) => (turn, phase, state))
            .ObserveOn(_dispatcherService.Scheduler)
            .Subscribe(_ =>
            {
                ClearSelection();
                UpdateGamePhase();
                NotifyStateChanged();
            });
    }

    /// <summary>
    /// Detaches the game event behind the command feedback. Safe to call more than once, and
    /// called from every teardown path so a detached view model never outlives its subscription.
    /// </summary>
    private void UnsubscribeFromCommandFeedback()
    {
        if (_commandFeedbackGame != null)
        {
            _commandFeedbackGame.CommandTimedOut -= OnCommandTimedOut;
            _commandFeedbackGame.CommandRejectedLocally -= OnCommandRejectedLocally;
        }
        _commandFeedbackGame = null;
    }

    /// <summary>
    /// Reports a command the client refused to send. These never reach the server, so no
    /// <see cref="ErrorCommand"/> comes back and this is the only way the player sees them.
    /// </summary>
    private void OnCommandRejectedLocally(ErrorCode errorCode)
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            CommandFeedbackLabel = string.Format(
                _localizationService.GetString("BattleMap_CommandRejected"),
                _localizationService.GetString($"Command_Error_{errorCode}"));
        });
    }

    private void OnCommandTimedOut()
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            CommandFeedbackLabel = _localizationService.GetString("BattleMap_CommandTimedOut");
        });
    }

    private void ProcessCommand(IGameCommand command)
    {
        if (Game == null) return;
        var formattedCommand = command.Render(_localizationService, Game);
        _commandLog.Add(formattedCommand);
        NotifyPropertyChanged(nameof(CommandLog));

        if (command is ErrorCommand)
        {
            CommandFeedbackLabel = string.Format(
                _localizationService.GetString("BattleMap_CommandRejected"),
                formattedCommand);
        }
        else if (CommandFeedbackLabel != null)
        {
            CommandFeedbackLabel = null;
        }

        switch (command)
        {
            case TurnIncrementedCommand turnCommand:
                _initiativeRolls.Clear();
                _announcedInitiativeWinnerId = null;
                AnnounceTurn(turnCommand.TurnNumber);
                break;
            case ChangePhaseCommand phaseCommand:
                _commandStreamPhase = phaseCommand.Phase;
                AnnouncePhase(phaseCommand.Phase);
                break;
            case ChangeActivePlayerCommand:
                AnnounceActivePlayer();
                break;
            case DiceRolledCommand diceRolledCommand when _commandStreamPhase == PhaseNames.Initiative:
                _initiativeRolls[diceRolledCommand.PlayerId] = diceRolledCommand.Roll;
                AnnounceInitiativeWinner(Game);
                break;
            case WeaponAttackDeclarationCommand weaponCommand:
                ProcessWeaponAttackDeclaration(weaponCommand);
                break;
            case WeaponAttackResolutionCommand resolutionCommand:
                ProcessWeaponAttackResolution(resolutionCommand);
                break;
            case MechStandUpCommand standUpCommand:
                ProcessMechStandUp(standUpCommand.UnitId, false);
                break;
            case MechFallCommand fallCommand:
                if (fallCommand.DamageData != null)
                    ProcessMechStandUp(fallCommand.UnitId, true);
                break;
            case GameEndedCommand gameEndedCommand:
                // Server ended the game - track state to allow UI to respond
                ProcessGameEnded(gameEndedCommand).SafeFireAndForget(ex =>
                    Game?.Logger.LogError(ex, "Error processing game ended command"));
                break;
            case BridgeCollapsedCommand:
                // Terrain mutation handled by ClientGame.OnBridgeCollapsed;
                // HexRenderControl re-renders via TerrainsChanged subscription
                break;
        }

        // Initiative does not publish PhaseStepChanges, so promote the state when
        // the server assigns the next player to roll.
        if (Game?.TurnPhase == PhaseNames.Initiative && command is ChangeActivePlayerCommand)
            UpdateGamePhase();

        NotifyStateChanged();
    }

    private void ProcessMechStandUp(Guid unitId, bool isFalling)
    {
        if (CurrentState is not MovementState movementState) return;
        if (isFalling)
        {
            movementState.ResumeMovementAfterFall(unitId);
            return;
        }

        movementState.ResumeMovementAfterStandup(unitId);
    }

    private void ProcessWeaponAttackDeclaration(WeaponAttackDeclarationCommand command)
    {
        if (Game == null) return;

        var attacker = Game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.UnitId);

        if (attacker?.Position == null || attacker.Owner == null) return;

        // Initialize the collection if it's null
        WeaponAttacks ??= [];

        // Dictionary to track offsets per target
        var targetOffsets = new Dictionary<Guid, int>();

        var newAttacks = command.WeaponTargets
            .Select(wt =>
            {
                var assignment = wt.Weapon.Assignments.FirstOrDefault();
                if (assignment is null) return null;

                var target = Game.Players
                    .SelectMany(p => p.Units)
                    .FirstOrDefault(u => u.Id == wt.TargetId);

                if (target?.Position == null) throw new Exception("The target should be deployed");

                // Get or initialize offset for this target
                var offset = targetOffsets.GetValueOrDefault(target.Id, 5);

                // Initial offset for new target
                // Get the actual weapon from the attacker
                var weapon = attacker.GetMountedComponentAtLocation<Weapon>(
                    assignment.Location,
                    assignment.FirstSlot);

                if (weapon == null) throw new Exception("The weapon is not found");

                var attack = new WeaponAttackViewModel
                {
                    From = attacker.Position!.Coordinates,
                    To = target.Position!.Coordinates,
                    Weapon = weapon,
                    AttackerTint = attacker.Owner.Tint,
                    LineOffset = offset,
                    TargetId = target.Id
                };

                // Increment and save offset for this target
                targetOffsets[target.Id] = offset + 5;

                return attack;
            })
            .Where(attack => attack is not null)
            .Select(attack => attack!)
            .ToList();

        WeaponAttacks.AddRange(newAttacks);
        NotifyPropertyChanged(nameof(WeaponAttacks));
    }

    private void ProcessWeaponAttackResolution(WeaponAttackResolutionCommand command)
    {
        if (Game == null || WeaponAttacks == null || !WeaponAttacks.Any()) return;

        var assignment = command.WeaponData.Assignments.FirstOrDefault();
        if (assignment is null) return;

        // Find and remove the attack that matches the weapon name and target ID
        var attacksToRemove = WeaponAttacks
            .Where(attack =>
                attack.Weapon.SlotAssignments.FirstOrDefault() is { } slotAssignment
                && slotAssignment.Location == assignment.Location
                && slotAssignment.FirstSlot == assignment.FirstSlot
                && attack.TargetId == command.TargetId)
            .ToList();

        if (attacksToRemove.Any())
        {
            foreach (var attack in attacksToRemove)
            {
                WeaponAttacks.Remove(attack);
            }

            NotifyPropertyChanged(nameof(WeaponAttacks));
        }
    }

    private void UpdateGamePhase()
    {
        if (Game?.PhaseStepState is not { } phaseState)
        {
            return;
        }

        var phase = Game.TurnPhase;
        switch (phase)
        {
            case PhaseNames.Initiative when phaseState.ActivePlayer != null:
                TransitionToState(new InitiativeState(this));
                break;
            case PhaseNames.Deployment when phaseState.ActivePlayer.Units.Any(u => !u.IsDeployed):
                TransitionToState(new DeploymentState(this));
                ShowUnitsToDeploy();
                break;

            case PhaseNames.Movement when phaseState.UnitsToPlay > 0:
                TransitionToState(new MovementState(this));
                break;

            case PhaseNames.WeaponsAttack when phaseState.UnitsToPlay > 0:
                TransitionToState(new WeaponsAttackState(this));
                break;

            case PhaseNames.End:
                ClearWeaponAttacks();
                TransitionToState(new EndState(this));
                break;

            default:
                TransitionToState(new IdleState());
                break;
        }
    }

    private void ClearWeaponAttacks()
    {
        // Clear weapon attacks during phase transitions
        if (WeaponAttacks?.Any() != true) return;
        WeaponAttacks.Clear();
        NotifyPropertyChanged(nameof(WeaponAttacks));
    }

    private void ShowUnitsToDeploy()
    {
        if (Game?.PhaseStepState == null || Game.PhaseStepState.Value.UnitsToPlay < 1)
        {
            UnitsToDeploy = [];
            return;
        }

        UnitsToDeploy = Game?.PhaseStepState?.ActivePlayer.Units.Where(u => !u.IsDeployed).ToList() ?? [];
    }

    private void TransitionToState(IUiState newState)
    {
        CurrentState = newState;
        NotifyStateChanged();
        NotifySelectedUnitChanged();
    }

    public void NotifyStateChanged()
    {
        NotifyPropertyChanged(nameof(Turn));
        NotifyPropertyChanged(nameof(TurnPhaseName));
        NotifyPropertyChanged(nameof(ActivePlayerName));
        NotifyPropertyChanged(nameof(IsLocalPlayerTurn));
        NotifyPropertyChanged(nameof(ActivePlayerTint));
        NotifyPropertyChanged(nameof(ActionInfoLabel));
        NotifyPropertyChanged(nameof(IsCommandFeedbackVisible));
        NotifyPropertyChanged(nameof(IsUserActionLabelVisible));
        NotifyPropertyChanged(nameof(AreUnitsToDeployVisible));
        NotifyPropertyChanged(nameof(WeaponSelectionItems));
        NotifyPropertyChanged(nameof(Attacker));
        NotifyPropertyChanged(nameof(IsPlayerActionButtonVisible));
        NotifyPropertyChanged(nameof(PlayerActionLabel));
        NotifyPropertyChanged(nameof(AvailableActions));

        // Update heat projection when the attacker changes
        HeatProjection.Unit = Attacker;
    }

    /// <summary>
    /// Adds a highlight to the specified hexes
    /// </summary>
    /// <param name="coordinates">The hex coordinates to highlight</param>
    /// <param name="highlightType">The type of highlight to add</param>
    internal void HighlightCoordinates(IReadOnlySet<HexCoordinates> coordinates, IHexHighlightType highlightType)
    {
        var hexesToHighlight = Game?.BattleMap?.GetHexes().Where(h => coordinates.Contains(h.Coordinates)).ToList();
        if (hexesToHighlight == null) return;
        UpdateHighlightBoundaryOutlines(coordinates, highlightType);
        foreach (var hex in hexesToHighlight)
        {
            hex.AddHighlight(highlightType);
        }
    }

    internal void HighlightRegions(
        IReadOnlyDictionary<HexCoordinates, IHexHighlightType> perHexHighlights)
    {
        if (perHexHighlights.Count == 0) return;

        if (TerrainBitmaskService == null)
        {
            ClearHighlightBoundaryOutlines();
        }
        else
        {
            // Group coordinates by highlight type for boundary computation
            var groups = new Dictionary<Type, (IHexHighlightType Highlight, HashSet<HexCoordinates> Coords)>();
            foreach (var (coord, highlight) in perHexHighlights)
            {
                var highlightType = highlight.GetType();
                if (!groups.TryGetValue(highlightType, out _))
                    groups[highlightType] = (highlight, []);
                groups[highlightType].Coords.Add(coord);
            }

            var merged = new Dictionary<HexCoordinates, HighlightBoundaryOutline>();
            foreach (var (highlight, coords) in groups.Values)
            {
                var outliner = ComputeBoundaryOutlines(coords, highlight);
                // Sets are disjoint per type, so simple addition is safe
                foreach (var (coord, outline) in outliner)
                    merged[coord] = outline;
            }

            _highlightBoundaryOutlines = merged;
            NotifyPropertyChanged(nameof(HighlightBoundaryOutlines));
        }

        // Apply per-hex highlights
        if (Game?.BattleMap == null) return;
        var hexMap = Game.BattleMap.GetHexes().ToDictionary(h => h.Coordinates);
        foreach (var (coord, highlight) in perHexHighlights)
        {
            if (hexMap.TryGetValue(coord, out var hex))
                hex.AddHighlight(highlight);
        }
    }

    /// <summary>
    /// Removes a specific highlight type from the specified hexes
    /// </summary>
    /// <param name="coordinates">The hex coordinates to remove highlight from</param>
    /// <typeparam name="T">The type of highlight to remove</typeparam>
    internal void RemoveHighlight<T>(IReadOnlySet<HexCoordinates> coordinates) where T : IHexHighlightType
    {
        var hexesToUnhighlight = Game?.BattleMap?.GetHexes().Where(h => coordinates.Contains(h.Coordinates)).ToList();
        if (hexesToUnhighlight == null) return;
        foreach (var hex in hexesToUnhighlight)
        {
            hex.RemoveHighlight<T>();
        }

        RemoveHighlightBoundaryOutlines(coordinates);
    }

    /// <summary>
    /// Clears all highlights from all hexes on the map
    /// </summary>
    internal void ClearHighlights()
    {
        var hexes = Game?.BattleMap?.GetHexes().ToList();
        if (hexes == null) return;
        foreach (var hex in hexes)
        {
            hex.ClearHighlights();
        }

        ClearHighlightBoundaryOutlines();
    }

    private void UpdateHighlightBoundaryOutlines(
        IReadOnlySet<HexCoordinates> coordinates,
        IHexHighlightType highlightType)
    {
        if (TerrainBitmaskService == null)
        {
            ClearHighlightBoundaryOutlines();
            return;
        }

        var newOutlines = ComputeBoundaryOutlines(coordinates, highlightType);
        var merged = new Dictionary<HexCoordinates, HighlightBoundaryOutline>(_highlightBoundaryOutlines);
        foreach (var (coord, outline) in newOutlines)
            merged[coord] = outline;
        _highlightBoundaryOutlines = merged;
        NotifyPropertyChanged(nameof(HighlightBoundaryOutlines));
    }

    private Dictionary<HexCoordinates, HighlightBoundaryOutline> ComputeBoundaryOutlines(
        IReadOnlySet<HexCoordinates> coordinates, IHexHighlightType highlightType)
    {
        const double outlineThickness = 2;
        return coordinates
            .Select(c => new
            {
                Coordinates = c,
                Mask = TerrainBitmaskService!.ComputeBoundaryMask(c, coordinates)
            })
            .Where(x => x.Mask != 0)
            .ToDictionary(x => x.Coordinates, x => new HighlightBoundaryOutline(x.Mask, highlightType, outlineThickness));
    }

    private void RemoveHighlightBoundaryOutlines(IReadOnlySet<HexCoordinates> coordinates)
    {
        if (_highlightBoundaryOutlines.Count == 0) return;

        _highlightBoundaryOutlines = _highlightBoundaryOutlines
            .Where(item => !coordinates.Contains(item.Key))
            .ToDictionary(item => item.Key, item => item.Value);

        NotifyPropertyChanged(nameof(HighlightBoundaryOutlines));
    }

    private void ClearHighlightBoundaryOutlines()
    {
        if (_highlightBoundaryOutlines.Count == 0) return;

        _highlightBoundaryOutlines = new Dictionary<HexCoordinates, HighlightBoundaryOutline>();
        NotifyPropertyChanged(nameof(HighlightBoundaryOutlines));
    }

    public List<IUnit> UnitsToDeploy
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(AreUnitsToDeployVisible));
        }
    } = [];

    public bool AreUnitsToDeployVisible => Game is not null
                                           && Game.CanActivePlayerAct
                                           && Game.PhaseStepState?.ActivePlayer.ControlType == PlayerControlType.Human
                                           && CurrentState is DeploymentState
                                           && UnitsToDeploy.Count > 0
                                           && SelectedUnit == null;

    public int Turn => Game?.Turn ?? 0;

    public string TurnPhaseName
    {
        get
        {
            var phase = Game?.TurnPhase ?? PhaseNames.Start;
            var key = $"Phase_{phase}";
            return _localizationService.GetString(key);
        }
    }

    public string ActivePlayerName => Game?.PhaseStepState?.ActivePlayer.Name ?? string.Empty;

    /// <summary>
    /// Indicates whether the active player is a local human player.
    /// </summary>
    public bool IsLocalPlayerTurn => Game is
        { PhaseStepState.ActivePlayer: { Id: var playerId, ControlType: PlayerControlType.Human } }
        && Game.LocalPlayers.Contains(playerId);

    /// <summary>
    /// Pending state-change announcements, in the order they should be shown. The banner control
    /// animates them one at a time and reports each one back through
    /// <see cref="TurnNotificationShownCommand"/>, which is what removes it.
    /// </summary>
    public ObservableCollection<TurnNotification> TurnNotifications { get; } = [];

    /// <summary>
    /// Invoked by the banner once a notification has finished animating.
    /// </summary>
    public ICommand TurnNotificationShownCommand { get; }

    private Guid? _announcedActivePlayerId;

    /// <summary>
    /// Queues an announcement, keeping simultaneous ones in <see cref="TurnNotificationKind"/>
    /// order so a new turn always reads turn, then phase, then whose turn it is, whatever order
    /// the server's commands arrive in.
    /// </summary>
    private void Announce(TurnNotification notification)
    {
        var rank = AnnouncementRank(notification.Kind);
        var index = 0;
        while (index < TurnNotifications.Count && AnnouncementRank(TurnNotifications[index].Kind) <= rank)
            index++;
        TurnNotifications.Insert(index, notification);
    }

    /// <summary>
    /// Where a notification sits relative to others queued at the same moment.
    ///
    /// A phase and a result of that phase share a rank deliberately. The initiative winner is only
    /// known once the last roll lands, which is after the initiative phase has been announced and
    /// before the movement phase is - and equal ranks queue in arrival order, so sharing a rank is
    /// what puts the result between the two phase banners. Ranking it ahead of phases announced it
    /// before the phase it reports on; ranking it behind them announced it after the next phase.
    /// </summary>
    private static int AnnouncementRank(TurnNotificationKind kind) => kind switch
    {
        TurnNotificationKind.Turn => 0,
        TurnNotificationKind.Phase or TurnNotificationKind.Initiative => 1,
        TurnNotificationKind.ActivePlayer => 2,
        _ => 3
    };

    private void AnnounceTurn(int turnNumber) => Announce(new TurnNotification(
        TurnNotificationKind.Turn,
        string.Format(_localizationService.GetString("BattleMap_Notification_Turn"), turnNumber)
            .ToUpperInvariant(),
        ActivePlayerTint));

    /// <summary>
    /// Announces a phase change, skipping the resolution phases: they are book-keeping steps
    /// rather than phases a player acts in.
    /// </summary>
    private void AnnouncePhase(PhaseNames phase)
    {
        if (phase.ToString().EndsWith("AttackResolution", StringComparison.Ordinal)) return;

        Announce(new TurnNotification(
            TurnNotificationKind.Phase,
            string.Format(_localizationService.GetString("BattleMap_Notification_Phase"),
                _localizationService.GetString($"Phase_{phase}")).ToUpperInvariant(),
            ActivePlayerTint));
    }

    /// <summary>
    /// Announces whose turn it is, once per change. Re-announcing the same player is suppressed:
    /// several commands can arrive while one player is still active.
    /// </summary>
    private void AnnounceActivePlayer()
    {
        if (Game?.PhaseStepState?.ActivePlayer is not { } activePlayer) return;
        if (_announcedActivePlayerId == activePlayer.Id) return;
        _announcedActivePlayerId = activePlayer.Id;

        var text = IsLocalPlayerTurn
            ? _localizationService.GetString("BattleMap_Notification_YourTurn")
            : string.Format(_localizationService.GetString("BattleMap_Notification_PlayersTurn"),
                activePlayer.Name);

        Announce(new TurnNotification(
            TurnNotificationKind.ActivePlayer,
            text.ToUpperInvariant(),
            activePlayer.Tint));
    }

    public string ActivePlayerTint => Game?.PhaseStepState?.ActivePlayer.Tint ?? "#FFFFFF";

    /// <summary>
    /// Announces who won initiative once every player has rolled and one of them is clear of the
    /// rest. A tie is re-rolled server side, so this runs again when the re-rolls land.
    /// </summary>
    private void AnnounceInitiativeWinner(IClientGame game)
    {
        if (_initiativeRolls.Count < game.AlivePlayers.Count) return;

        var winner = GetInitiativeWinner(game);
        if (winner == null || _announcedInitiativeWinnerId == winner.Id) return;

        _announcedInitiativeWinnerId = winner.Id;
        Announce(new TurnNotification(
            TurnNotificationKind.Initiative,
            string.Format(_localizationService.GetString("BattleMap_Notification_InitiativeWinner"),
                winner.Name, _initiativeRolls[winner.Id]).ToUpperInvariant(),
            winner.Tint));
    }

    /// <summary>
    /// The single highest roller, or null when the highest roll is tied. Only called once a roll
    /// has been recorded, so the roll set is never empty here.
    /// </summary>
    private IPlayer? GetInitiativeWinner(IClientGame game)
    {
        var highestRoll = _initiativeRolls.Values.Max();
        var winners = _initiativeRolls
            .Where(result => result.Value == highestRoll)
            .Select(result => game.Players.FirstOrDefault(player => player.Id == result.Key))
            .OfType<IPlayer>()
            .ToList();

        return winners.Count == 1 ? winners[0] : null;
    }

    public bool AreActionsMenuOffMap => _platformService.IsMobile;

    /// <summary>
    /// Returns the available actions for the current UI state.
    /// Used on mobile to render action buttons in a fixed position overlay.
    /// </summary>
    public IEnumerable<StateAction> AvailableActions => CurrentState.GetAvailableActions();

    public IImageService ImageService { get; }

    public IUnit? SelectedUnit
    {
        get => CurrentState.SelectedUnit;
        set
        {
            if (value != null && !CurrentState.CanSelectUnit(value))
                return;
            CurrentState.HandleUnitSelectionFromList(value);
            NotifySelectedUnitChanged();
        }
    }

    public void NotifySelectedUnitChanged()
    {
        NotifyPropertyChanged(nameof(SelectedUnit));
        NotifyPropertyChanged(nameof(AreUnitsToDeployVisible));
        NotifyPropertyChanged(nameof(IsRecordSheetButtonVisible));
        NotifyPropertyChanged(nameof(IsRecordSheetPanelVisible));

        UpdateSelectedUnitEvents();

        // Update heat projection for a selected unit
        SelectedUnitHeatProjection.Unit = SelectedUnit;
    }

    public IUnit? Attacker =>
        CurrentState is WeaponsAttackState weaponsAttackState ? weaponsAttackState.Attacker : null;

    public void HandleHexSelection(Hex selectedHex)
    {
        CurrentState.HandleHexSelection(selectedHex);
    }

    /// <summary>
    /// Resolves a click position in map content pixels to a hex and routes
    /// the selection to the current UI state.
    /// </summary>
    public void SelectHexAt(double x, double y)
    {
        if (Game?.BattleMap == null) return;
        var coords = HexCoordinatesPixelExtensions.FromPixel(x, y);
        var hex = Game.BattleMap.GetHexes()
            .FirstOrDefault(h => h.Coordinates == coords);
        if (hex != null)
            HandleHexSelection(hex);
    }

    private void ClearSelection()
    {
        SelectedUnit = null;
    }

    public string ActionInfoLabel => CurrentState.ActionLabel;
    public bool IsUserActionLabelVisible => CurrentState.IsActionRequired;

    public string PlayerActionLabel => CurrentState.PlayerActionLabel;

    public bool IsPlayerActionButtonVisible =>
        CurrentState.CanExecutePlayerAction;

    public void HandlePlayerAction()
    {
        CurrentState.ExecutePlayerAction();
    }

    public HexRenderConfigurationViewModel HexConfiguration { get; }

    public bool IsCommandLogExpanded
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsRecordSheetExpanded
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(IsRecordSheetButtonVisible));
            NotifyPropertyChanged(nameof(IsRecordSheetPanelVisible));
        }
    }

    public bool IsMapSettingsPanelVisible
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsRecordSheetButtonVisible => SelectedUnit != null && !IsRecordSheetExpanded;
    public bool IsRecordSheetPanelVisible => SelectedUnit != null && IsRecordSheetExpanded;

    public void ToggleCommandLog()
    {
        IsCommandLogExpanded = !IsCommandLogExpanded;
    }

    public void ToggleRecordSheet()
    {
        IsRecordSheetExpanded = !IsRecordSheetExpanded;
    }

    public void ToggleMapSettings()
    {
        IsMapSettingsPanelVisible = !IsMapSettingsPanelVisible;
    }

    public IEnumerable<IUnit> Units => Game?.AlivePlayers.SelectMany(p => p.AliveUnits) ?? [];

    public IUiState CurrentState { get; private set; }

    public void ShowDirectionSelector(HexCoordinates position, IEnumerable<HexDirection> availableDirections)
    {
        DirectionSelectorPosition = position;
        AvailableDirections = availableDirections;
        IsDirectionSelectorVisible = true;
        NotifyStateChanged();
    }

    public void HideDirectionSelector()
    {
        IsDirectionSelectorVisible = false;
        AvailableDirections = null;
    }

    public void ShowMovementPath(MovementPath? path)
    {
        HideMovementPath();
        if (path == null || path.TotalCost == 0)
        {
            return;
        }

        var segments = path.Segments.Select(p => new PathSegmentViewModel(p)).ToList();
        MovementPath = segments;
    }

    public void HideMovementPath()
    {
        MovementPath = null;
    }

    /// <summary>
    /// List of UI events for the selected unit
    /// </summary>
    public IReadOnlyList<UiEventViewModel> SelectedUnitEvents => _selectedUnitEvents;

    /// <summary>
    /// Updates the list of UI events for the selected unit
    /// </summary>
    private void UpdateSelectedUnitEvents()
    {
        if (SelectedUnit == null)
        {
            _selectedUnitEvents = [];
        }
        else
        {
            _selectedUnitEvents = SelectedUnit.Events
                .Select(e => new UiEventViewModel(e, _localizationService))
                .ToList();
        }

        NotifyPropertyChanged(nameof(SelectedUnitEvents));
    }

    /// <summary>
    /// Shows the body part selector for aimed shots
    /// </summary>
    public void ShowAimedShotLocationSelector(AimedShotLocationSelectorViewModel aimedShotLocationSelector)
    {
        UnitPartSelector = aimedShotLocationSelector;
        IsUnitPartSelectorVisible = true;
    }

    /// <summary>
    /// Hides the body part selector
    /// </summary>
    public void HideAimedShotLocationSelector()
    {
        UnitPartSelector = null;
        IsUnitPartSelectorVisible = false;
    }

    /// <summary>
    /// Command to hide the body part selector
    /// </summary>
    public ICommand HideBodyPartSelectorCommand { get; }

    public ICommand LeaveGameCommand { get; }

    /// <summary>
    /// Callback provided by the view to capture the current map as PNG bytes.
    /// Returns (PngBytes, WidthPixels, HeightPixels).
    /// </summary>
    public Func<Task<(byte[] PngBytes, int WidthPixels, int HeightPixels)>>? CaptureMap { get; set; }

    /// <summary>
    /// Callback provided by the view to center the map viewport.
    /// </summary>
    public Action? CenterMap { get; set; }

    public IAsyncCommand CenterMapCommand => field ??= new AsyncCommand(() =>
    {
        CenterMap?.Invoke();
        return Task.CompletedTask;
    });

    public IAsyncCommand ExportMapToPdfCommand => field ??= new AsyncCommand(async () =>
    {
        if (CaptureMap == null || _pdfExportService == null || _fileService == null) return;
        try
        {
            var (pngBytes, widthPixels, heightPixels) = await CaptureMap();
            if (pngBytes.Length == 0) return;
            var widthPoints = widthPixels * 72 / 96;
            var heightPoints = heightPixels * 72 / 96;
            var pdfBytes = await _pdfExportService.GeneratePdfFromPngAsync(
                pngBytes, widthPoints, heightPoints);
            await _fileService.SaveBinaryFile(
                _localizationService.GetString("BattleMap_ExportPdfDialogTitle"),
                "map.pdf",
                pdfBytes,
                "pdf",
                "PDF files");
        }
        catch (Exception ex)
        {
            Game?.Logger.LogError(ex, "PDF map export failed");
        }
    });

    public bool CanExportPdf =>
#if DEBUG
        true;
#else
        false;
#endif

    public ITerrainAssetService TerrainAssetService { get; }

    public ITerrainBitmaskService? TerrainBitmaskService { get; }

    public IScheduler Scheduler => _dispatcherService.Scheduler;

    private async Task ProcessGameEnded(GameEndedCommand command)
    {
        // A terminated connection may already have ended the game (e.g. via the
        // transport-closed flow); do not show a second interruption dialog.
        if (IsGameOver) return;
        IsGameOver = true;
        GameEndReason = command.Reason;
        NotifyStateChanged();

        if (GameEndReason == GameEndReason.Victory)
        {
            // The victory flow is handled by the End phase / EndGameViewModel
            return;
        }

        // If the local client caused the leave, suppress the interruption dialog.
        // The LeaveGame() flow already performs navigation for this client.
        if (GameEndReason == GameEndReason.PlayersLeft
            && command.PlayerId.HasValue
            && Game?.LocalPlayers.Contains(command.PlayerId.Value) == true)
        {
            return;
        }

        // The game ended because of an interruption (e.g. the host disconnected).
        // Inform the player with a single-button dialog and return to the main menu.
        var okAction = new UiAction
        {
            Title = _localizationService.GetString("Dialog_Ok")
        };

        var (title, message) = GameEndReason == GameEndReason.HostDisconnected
            ? (_localizationService.GetString("Dialog_HostDisconnected_Title"),
                _localizationService.GetString("Dialog_HostDisconnected_Message"))
            : (_localizationService.GetString("Dialog_GameInterrupted_Title"),
                _localizationService.GetString("Dialog_GameInterrupted_Message"));

        var selectedAction = await NavigationService.AskForActionAsync(title, message, okAction);

        if (selectedAction != okAction) return;

        await GoToMainMenu();
    }

    public async Task NavigateToEndGame()
    {
        // If the game ended with victory, navigate to the end game screen
        // For other reasons navigate back to a menu
        if (GameEndReason != GameEndReason.Victory || _game == null)
        {
            await GoToMainMenu();
            return;
        }

        var endGameViewModel = await NavigationService.GetNewViewModelAsync<EndGameViewModel>();
        if (endGameViewModel == null)
        {
            await GoToMainMenu();
            return;
        }

        // Initialize the end game view model with the game and reason
        endGameViewModel.Initialize(_game, GameEndReason);
        await NavigationService.NavigateToViewModelAsync(endGameViewModel);
    }

    public void Dispose()
    {
        HexConfiguration.PropertyChanged -= _hexConfigurationChangedHandler;
        ConnectionStatus.PropertyChanged -= OnConnectionStatusPropertyChanged;
        ConnectionStatus.Dispose();
        _gameSubscription?.Dispose();
        _commandSubscription?.Dispose();
        UnsubscribeFromCommandFeedback();
        if (Game is { IsDisposed: false })
        {
            Game.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    public override void AttachHandlers()
    {
        base.AttachHandlers();
        // Restore game/command subscriptions if the view was re-attached
        // (e.g. re-navigation recreated the view and DetachHandlers disposed them).
        SubscribeToGameChanges();
        if (_connectionStatusSource != null)
        {
            ConnectionStatus.Subscribe(_connectionStatusSource, Scheduler);
        }

        if (_hexConfigurationChangedHandler != null)
        {
            HexConfiguration.PropertyChanged += _hexConfigurationChangedHandler;
        }
    }

    public override void DetachHandlers()
    {
        HexConfiguration.PropertyChanged -= _hexConfigurationChangedHandler;
        base.DetachHandlers();
        _gameSubscription?.Dispose();
        _commandSubscription?.Dispose();
        UnsubscribeFromCommandFeedback();
        ConnectionStatus.Subscribe(null, Scheduler);
    }

    private async Task GoToMainMenu()
    {
        // Dispose of the game
        if (Game != null)
        {
            Game.Dispose();
            Game = null;
        }

        await NavigationService.NavigateToRootAsync();
    }
}
