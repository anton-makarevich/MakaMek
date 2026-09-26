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
    private bool _playerActionConfirmationPending;
    private readonly Dictionary<Guid, int> _initiativeRolls = [];
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
        CloseOverlayPanels();
        CloseActionSelectors();
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
        ILogger<ConnectionStatusViewModel>? connectionLogger = null)
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
        InspectUnitCommand = new AsyncCommand<IUnit>(unit =>
        {
            if (unit != null)
                InspectUnit(unit);
            return Task.CompletedTask;
        });
        FocusUnitCommand = new AsyncCommand<IUnit>(unit =>
        {
            if (unit != null)
                FocusUnit?.Invoke(unit);
            return Task.CompletedTask;
        });
        NextAvailableUnitCommand = new AsyncCommand(SelectNextAvailableUnit);
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

    /// <summary>
    /// Gets the number of weapons currently selected for the pending attack.
    /// </summary>
    public int SelectedAttackWeaponCount => WeaponSelectionItems.Count(weapon => weapon.IsSelected);

    /// <summary>
    /// Gets the heat that the currently selected weapons will generate.
    /// </summary>
    public int SelectedAttackHeat => WeaponSelectionItems
        .Where(weapon => weapon.IsSelected)
        .Sum(weapon => weapon.Weapon.Heat);

    /// <summary>
    /// Gets the number of ammunition shots consumed by the currently selected weapons.
    /// </summary>
    public int SelectedAttackAmmo => WeaponSelectionItems
        .Count(weapon => weapon.IsSelected && weapon.RequiresAmmo);

    /// <summary>
    /// Gets whether the selected-attack summary should be shown.
    /// </summary>
    public bool IsAttackSelectionSummaryVisible =>
        CurrentState is WeaponsAttackState && SelectedAttackWeaponCount > 0;

    /// <summary>
    /// Gets a compact summary of the selected attack's resource costs.
    /// </summary>
    public string AttackSelectionSummaryText => string.Format(
        LocalizationService.GetString("WeaponSelection_AttackSummary"),
        SelectedAttackWeaponCount,
        SelectedAttackHeat,
        SelectedAttackAmmo);

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
        if (_commandFeedbackGame != null)
        {
            _commandFeedbackGame.CommandTimedOut -= OnCommandTimedOut;
        }
        _commandFeedbackGame = null;

        if (Game is null) return;

        _commandFeedbackGame = Game;
        _commandFeedbackGame.CommandTimedOut += OnCommandTimedOut;

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

    private void OnCommandTimedOut()
    {
        _dispatcherService.RunOnUIThread(() =>
        {
            CommandFeedbackLabel = _localizationService.GetString("BattleMap_CommandTimedOut");
            NotifyPropertyChanged(nameof(TurnActionStatusLabel));
            NotifyPropertyChanged(nameof(IsTurnActionPanelVisible));
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
            case TurnIncrementedCommand:
                _initiativeRolls.Clear();
                break;
            case DiceRolledCommand diceRolledCommand when Game.TurnPhase == PhaseNames.Initiative:
                _initiativeRolls[diceRolledCommand.PlayerId] = diceRolledCommand.Roll;
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
                var target = Game.Players
                    .SelectMany(p => p.Units)
                    .FirstOrDefault(u => u.Id == wt.TargetId);

                if (target?.Position == null) throw new Exception("The target should be deployed");

                // Get or initialize offset for this target
                var offset = targetOffsets.GetValueOrDefault(target.Id, 5);

                // Initial offset for new target
                // Get the actual weapon from the attacker
                var weapon = attacker.GetMountedComponentAtLocation<Weapon>(
                    wt.Weapon.Assignments.First().Location,
                    wt.Weapon.Assignments.First().FirstSlot);

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
            .ToList();

        WeaponAttacks.AddRange(newAttacks);
        NotifyPropertyChanged(nameof(WeaponAttacks));
    }

    private void ProcessWeaponAttackResolution(WeaponAttackResolutionCommand command)
    {
        if (Game == null || WeaponAttacks == null || !WeaponAttacks.Any()) return;

        // Find and remove the attack that matches the weapon name and target ID
        var attacksToRemove = WeaponAttacks
            .Where(attack =>
                attack.Weapon.SlotAssignments[0].Location == command.WeaponData.Assignments[0].Location
                && attack.Weapon.SlotAssignments[0].FirstSlot == command.WeaponData.Assignments[0].FirstSlot
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
        _playerActionConfirmationPending = false;
        NotifyPropertyChanged(nameof(Turn));
        NotifyPropertyChanged(nameof(TurnPhaseName));
        NotifyPropertyChanged(nameof(ActivePlayerName));
        NotifyPropertyChanged(nameof(ActivePlayerTint));
        NotifyPropertyChanged(nameof(IsInitiativeResultVisible));
        NotifyPropertyChanged(nameof(InitiativeOutcomeLabel));
        NotifyPropertyChanged(nameof(InitiativeWinnerTint));
        NotifyPropertyChanged(nameof(IsInitiativeStatusVisible));
        NotifyPropertyChanged(nameof(InitiativeStatusLabel));
        NotifyPropertyChanged(nameof(TurnGuidanceLabel));
        NotifyPropertyChanged(nameof(ActionInfoLabel));
        NotifyPropertyChanged(nameof(IsTurnActionPanelVisible));
        NotifyPropertyChanged(nameof(TurnActionStatusLabel));
        NotifyPropertyChanged(nameof(ActiveUnitLabel));
        NotifyPropertyChanged(nameof(IsCommandFeedbackVisible));
        NotifyPropertyChanged(nameof(IsUserActionLabelVisible));
        NotifyPropertyChanged(nameof(AreUnitsToDeployVisible));
        NotifyPropertyChanged(nameof(WeaponSelectionItems));
        NotifyPropertyChanged(nameof(SelectedAttackWeaponCount));
        NotifyPropertyChanged(nameof(SelectedAttackHeat));
        NotifyPropertyChanged(nameof(SelectedAttackAmmo));
        NotifyPropertyChanged(nameof(IsAttackSelectionSummaryVisible));
        NotifyPropertyChanged(nameof(AttackSelectionSummaryText));
        NotifyPropertyChanged(nameof(IsAttackOverlayVisible));
        NotifyPropertyChanged(nameof(Attacker));
        NotifyPropertyChanged(nameof(IsPlayerActionButtonVisible));
        NotifyPropertyChanged(nameof(PlayerActionLabel));
        NotifyPropertyChanged(nameof(AvailableActions));
        NotifyPropertyChanged(nameof(LocalUnits));
        NotifyPropertyChanged(nameof(IsSquadStatusBarVisible));
        NotifyPropertyChanged(nameof(IsNextAvailableUnitVisible));

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

    public string ActivePlayerTint => Game?.PhaseStepState?.ActivePlayer.Tint ?? "#FFFFFF";

    /// <summary>
    /// Gives the player the most useful next-step context for the current turn phase.
    /// </summary>
    public string TurnGuidanceLabel
    {
        get
        {
            var phase = Game?.TurnPhase;
            var unitsToPlay = Game?.PhaseStepState?.UnitsToPlay ?? 0;
            return phase switch
            {
                PhaseNames.Movement or PhaseNames.WeaponsAttack when unitsToPlay > 0 =>
                    string.Format(_localizationService.GetString("BattleMap_UnitsRemaining"), unitsToPlay),
                PhaseNames.End => _localizationService.GetString("BattleMap_EndTurnGuidance"),
                _ => string.Empty
            };
        }
    }

    /// <summary>
    /// Gets whether the completed initiative result should be shown in the turn banner.
    /// </summary>
    public bool IsInitiativeResultVisible => _initiativeRolls.Count > 0
                                              && Game?.TurnPhase is not null and not PhaseNames.Initiative;

    /// <summary>
    /// Gets whether the turn banner should explain initiative while it is rolling or after it resolves.
    /// </summary>
    public bool IsInitiativeStatusVisible => Game?.TurnPhase == PhaseNames.Initiative || IsInitiativeResultVisible;

    /// <summary>
    /// Gets the current initiative progress or the resolved winner summary.
    /// </summary>
    public string InitiativeStatusLabel
    {
        get
        {
            if (Game?.TurnPhase == PhaseNames.Initiative)
            {
                return string.Format(_localizationService.GetString("BattleMap_InitiativeProgress"),
                    _initiativeRolls.Count, Game.AlivePlayers.Count);
            }

            return InitiativeOutcomeLabel;
        }
    }

    /// <summary>
    /// Gets the localized summary identifying the player who won initiative this turn.
    /// </summary>
    public string InitiativeOutcomeLabel
    {
        get
        {
            var winner = GetInitiativeWinner();
            return winner == null
                ? string.Empty
                : string.Format(_localizationService.GetString("BattleMap_InitiativeWinner"), winner.Name,
                    _initiativeRolls[winner.Id]);
        }
    }

    /// <summary>
    /// Gets the tint of the player who won initiative, for visual reinforcement.
    /// </summary>
    public string InitiativeWinnerTint => GetInitiativeWinner()?.Tint ?? ActivePlayerTint;

    private IPlayer? GetInitiativeWinner()
    {
        if (_initiativeRolls.Count == 0 || Game == null) return null;

        var highestRoll = _initiativeRolls.Values.Max();
        var winners = _initiativeRolls
            .Where(result => result.Value == highestRoll)
            .Select(result => Game.Players.FirstOrDefault(player => player.Id == result.Key))
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

        if (IsRecordSheetExpanded)
            InspectedUnit = SelectedUnit;

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

    /// <summary>
    /// Gets whether the turn-action panel has useful phase or command status to show.
    /// </summary>
    public bool IsTurnActionPanelVisible => !string.IsNullOrWhiteSpace(TurnActionStatusLabel)
                                            || !string.IsNullOrWhiteSpace(ActiveUnitLabel)
                                            || !string.IsNullOrWhiteSpace(TurnGuidanceLabel);

    /// <summary>
    /// Gets the primary action or the reason the player must wait before acting.
    /// </summary>
    public string TurnActionStatusLabel
    {
        get
        {
            if (Game is not { } game || game.PhaseStepState?.ActivePlayer is not { } activePlayer)
                return string.Empty;

            if (game.LocalPlayers.Contains(activePlayer.Id))
                return ActionInfoLabel;

            return string.Format(_localizationService.GetString("BattleMap_WaitingForPlayer"), activePlayer.Name);
        }
    }

    /// <summary>
    /// Gets the unit currently driving the active movement or attack action.
    /// </summary>
    public string ActiveUnitLabel
    {
        get
        {
            var unit = CurrentState.SelectedUnit ?? Attacker;
            return unit == null
                ? string.Empty
                : string.Format(_localizationService.GetString("BattleMap_ActiveUnit"), unit.Name);
        }
    }

    public string PlayerActionLabel => _playerActionConfirmationPending
        ? _localizationService.GetString("BattleMap_ConfirmEndTurn")
        : CurrentState.PlayerActionLabel;

    public bool IsPlayerActionButtonVisible =>
        CurrentState.CanExecutePlayerAction;

    public void HandlePlayerAction()
    {
        if (CurrentState is EndState && !_playerActionConfirmationPending)
        {
            _playerActionConfirmationPending = true;
            NotifyPropertyChanged(nameof(PlayerActionLabel));
            return;
        }

        _playerActionConfirmationPending = false;
        CurrentState.ExecutePlayerAction();
    }

    /// <summary>
    /// Cancels a pending end-turn confirmation without changing game state.
    /// </summary>
    public void CancelPlayerActionConfirmation()
    {
        if (!_playerActionConfirmationPending) return;
        _playerActionConfirmationPending = false;
        NotifyPropertyChanged(nameof(PlayerActionLabel));
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
            if (value && !field)
            {
                IsCommandLogExpanded = false;
                IsMapSettingsPanelVisible = false;
            }

            SetProperty(ref field, value);
            if (value && InspectedUnit == null)
                InspectedUnit = SelectedUnit;
            NotifyPropertyChanged(nameof(IsRecordSheetButtonVisible));
            NotifyPropertyChanged(nameof(IsRecordSheetPanelVisible));
        }
    }

    /// <summary>
    /// Gets or sets whether the inspected-unit drawer should remain open until explicitly unpinned.
    /// </summary>
    public bool IsRecordSheetPinned
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsMapSettingsPanelVisible
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Gets or sets whether the map navigation and utility drawer is open.
    /// </summary>
    public bool IsMapControlsDrawerOpen
    {
        get;
        set => SetProperty(ref field, value);
    }

    /// <summary>
    /// Gets whether the firing-arc legend should be visible over the map.
    /// </summary>
    public bool IsAttackOverlayVisible => CurrentState is WeaponsAttackState && Attacker != null;

    public bool IsRecordSheetButtonVisible => SelectedUnit != null && !IsRecordSheetExpanded;
    public bool IsRecordSheetPanelVisible => InspectedUnit != null && IsRecordSheetExpanded;

    /// <summary>
    /// Gets the unit currently shown in the information drawer. This is separate from
    /// <see cref="SelectedUnit"/> so browsing the squad bar does not change phase actions.
    /// </summary>
    public IUnit? InspectedUnit
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(IsRecordSheetPanelVisible));
        }
    }

    /// <summary>
    /// Gets the local player's living units for the persistent squad status bar.
    /// </summary>
    public IEnumerable<IUnit> LocalUnits => Game?.Players
        .Where(player => Game.LocalPlayers.Contains(player.Id))
        .SelectMany(player => player.Units) ?? [];

    public bool IsSquadStatusBarVisible => LocalUnits.Any();

    /// <summary>
    /// Gets whether the squad contains a living, non-shutdown unit to navigate to.
    /// </summary>
    public bool IsNextAvailableUnitVisible => LocalUnits.Any(unit => !unit.IsOutOfCommission && !unit.IsShutdown);

    /// <summary>
    /// Selects, inspects, and centers the next available local unit in squad order.
    /// </summary>
    public Task SelectNextAvailableUnit()
    {
        var availableUnits = LocalUnits
            .Where(unit => !unit.IsOutOfCommission && !unit.IsShutdown)
            .ToList();
        if (availableUnits.Count == 0) return Task.CompletedTask;

        var currentIndex = SelectedUnit is { } selectedUnit
            ? availableUnits.IndexOf(selectedUnit)
            : -1;
        var nextUnit = availableUnits[(currentIndex + 1) % availableUnits.Count];

        if (CurrentState.CanSelectUnit(nextUnit))
            SelectedUnit = nextUnit;
        InspectUnit(nextUnit);
        FocusUnit?.Invoke(nextUnit);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Opens the information drawer for a unit without changing the active phase selection.
    /// </summary>
    public void InspectUnit(IUnit unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        InspectedUnit = unit;
        IsRecordSheetExpanded = true;
    }

    /// <summary>
    /// Closes the inspected-unit drawer when another overlay takes focus.
    /// Opening a different panel is an explicit navigation choice, so it also
    /// dismisses a pinned drawer without changing the pin preference.
    /// </summary>
    private void CloseRecordSheet()
    {
        if (!IsRecordSheetExpanded) return;

        IsRecordSheetExpanded = false;
        InspectedUnit = null;
    }

    /// <summary>
    /// Ensures action selectors and utility panels have exclusive screen space.
    /// </summary>
    private void CloseOverlayPanels()
    {
        CloseRecordSheet();
        IsCommandLogExpanded = false;
        IsMapSettingsPanelVisible = false;
    }

    /// <summary>
    /// Ensures only one map interaction selector is active at a time.
    /// </summary>
    private void CloseActionSelectors()
    {
        HideDirectionSelector();
        HideSurfaceSelector();
        HideAimedShotLocationSelector();
    }

    /// <summary>
    /// Opens or closes the command log while keeping other overlays exclusive.
    /// </summary>
    public void ToggleCommandLog()
    {
        var shouldExpand = !IsCommandLogExpanded;
        if (shouldExpand)
        {
            CloseRecordSheet();
            IsMapSettingsPanelVisible = false;
        }

        IsCommandLogExpanded = shouldExpand;
    }

    public void ToggleRecordSheet()
    {
        if (IsRecordSheetExpanded && IsRecordSheetPinned) return;
        if (!IsRecordSheetExpanded)
            InspectedUnit = SelectedUnit;
        else
            InspectedUnit = null;
        IsRecordSheetExpanded = !IsRecordSheetExpanded;
    }

    /// <summary>
    /// Toggles protection against accidentally closing the inspected-unit drawer.
    /// </summary>
    public void ToggleRecordSheetPin()
    {
        IsRecordSheetPinned = !IsRecordSheetPinned;
    }

    /// <summary>
    /// Opens or closes map settings while keeping other overlays exclusive.
    /// </summary>
    public void ToggleMapSettings()
    {
        var shouldShow = !IsMapSettingsPanelVisible;
        if (shouldShow)
        {
            CloseRecordSheet();
            IsCommandLogExpanded = false;
        }

        IsMapSettingsPanelVisible = shouldShow;
    }

    /// <summary>
    /// Opens or closes the map navigation and utility drawer.
    /// </summary>
    public void ToggleMapControlsDrawer()
    {
        IsMapControlsDrawerOpen = !IsMapControlsDrawerOpen;
    }

    public IEnumerable<IUnit> Units => Game?.AlivePlayers.SelectMany(p => p.AliveUnits) ?? [];

    public ICommand InspectUnitCommand { get; }

    /// <summary>
    /// Gets the command used by squad cards to center a unit on the map.
    /// </summary>
    public ICommand FocusUnitCommand { get; }

    /// <summary>
    /// Gets the command that advances to the next available local unit.
    /// </summary>
    public ICommand NextAvailableUnitCommand { get; }

    /// <summary>
    /// Callback assigned by the map view to pan to a unit without changing map zoom.
    /// </summary>
    public Action<IUnit>? FocusUnit { get; set; }

    public IUiState CurrentState { get; private set; }

    public void ShowDirectionSelector(HexCoordinates position, IEnumerable<HexDirection> availableDirections)
    {
        CloseOverlayPanels();
        CloseActionSelectors();
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
        CloseOverlayPanels();
        CloseActionSelectors();
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

    /// <summary>Callback provided by the view to zoom in around the viewport center.</summary>
    public Action? ZoomIn { get; set; }

    /// <summary>Callback provided by the view to zoom out around the viewport center.</summary>
    public Action? ZoomOut { get; set; }

    /// <summary>Callback provided by the view to fit the complete map in the viewport.</summary>
    public Action? FitMap { get; set; }

    public IAsyncCommand CenterMapCommand => field ??= new AsyncCommand(() =>
    {
        CenterMap?.Invoke();
        return Task.CompletedTask;
    });

    public IAsyncCommand ZoomInCommand => field ??= new AsyncCommand(() =>
    {
        ZoomIn?.Invoke();
        return Task.CompletedTask;
    });

    public IAsyncCommand ZoomOutCommand => field ??= new AsyncCommand(() =>
    {
        ZoomOut?.Invoke();
        return Task.CompletedTask;
    });

    public IAsyncCommand FitMapCommand => field ??= new AsyncCommand(() =>
    {
        FitMap?.Invoke();
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
        if (_commandFeedbackGame != null)
        {
            _commandFeedbackGame.CommandTimedOut -= OnCommandTimedOut;
            _commandFeedbackGame = null;
        }
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
