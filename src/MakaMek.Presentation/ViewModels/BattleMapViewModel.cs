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
/// <param name="Color">The outline color string.</param>
/// <param name="Thickness">The outline stroke thickness.</param>
public sealed record HighlightBoundaryOutline(byte EdgeMask, string Color, double Thickness);

public class BattleMapViewModel : BaseViewModel, IDisposable
{
    private IClientGame? _game;
    private IDisposable? _gameSubscription;
    private IDisposable? _commandSubscription;
    private readonly ObservableCollection<string> _commandLog = [];
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IPlatformService _platformService;
    private readonly IPdfExportService? _pdfExportService;
    private readonly IFileService? _fileService;
    private List<UiEventViewModel> _selectedUnitEvents = [];
    private readonly PropertyChangedEventHandler? _hexConfigurationChangedHandler;
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
        ITerrainBitmaskService? terrainBitmaskService = null)
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
                if (Game==null || Game.IsDisposed) return;
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
            SetProperty(ref _game, value);
            SubscribeToGameChanges();
        }
    }
    
    public ILocalizationService LocalizationService => _localizationService;

    public IReadOnlyCollection<string> CommandLog => _commandLog;

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
        
        if (Game is null) return;

        _commandSubscription = Game.Commands
            .ObserveOn(_dispatcherService.Scheduler)
            .Subscribe( ProcessCommand );
        
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

    private void ProcessCommand(IGameCommand command)
    {
        if (Game == null) return;
        var formattedCommand = command.Render(_localizationService, Game);
        _commandLog.Add(formattedCommand);
        NotifyPropertyChanged(nameof(CommandLog));

        switch (command)
        {
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
                ProcessGameEnded(gameEndedCommand).SafeFireAndForget(ex => Game?.Logger.LogError(ex, "Error processing game ended command"));
                break;
            case BridgeCollapsedCommand:
                // Terrain mutation handled by ClientGame.OnBridgeCollapsed;
                // HexRenderControl re-renders via TerrainsChanged subscription
                break;
        }

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
        UnitsToDeploy = Game?.PhaseStepState?.ActivePlayer.Units.Where(u => !u.IsDeployed).ToList()??[];
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
        NotifyPropertyChanged(nameof(ActivePlayerTint));
        NotifyPropertyChanged(nameof(ActionInfoLabel));
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
            var groups = new Dictionary<Type, (string Color, HashSet<HexCoordinates> Coords)>();
            foreach (var (coord, highlight) in perHexHighlights)
            {
                var highlightType = highlight.GetType();
                if (!groups.TryGetValue(highlightType, out _))
                    groups[highlightType] = (GetBoundaryOutlineColor(highlight), []);
                groups[highlightType].Coords.Add(coord);
            }

            var merged = new Dictionary<HexCoordinates, HighlightBoundaryOutline>();
            foreach (var (color, coords) in groups.Values)
            {
                var outliner = ComputeBoundaryOutlines(coords, color);
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

        var outlineColor = GetBoundaryOutlineColor(highlightType);
        var newOutlines = ComputeBoundaryOutlines(coordinates, outlineColor);
        var merged = new Dictionary<HexCoordinates, HighlightBoundaryOutline>(_highlightBoundaryOutlines);
        foreach (var (coord, outline) in newOutlines)
            merged[coord] = outline;
        _highlightBoundaryOutlines = merged;
        NotifyPropertyChanged(nameof(HighlightBoundaryOutlines));
    }

    private Dictionary<HexCoordinates, HighlightBoundaryOutline> ComputeBoundaryOutlines(
        IReadOnlySet<HexCoordinates> coordinates, string color)
    {
        const double outlineThickness = 2;
        return coordinates
            .Select(c => new
            {
                Coordinates = c,
                Mask = TerrainBitmaskService!.ComputeBoundaryMask(c, coordinates)
            })
            .Where(x => x.Mask != 0)
            .ToDictionary(x => x.Coordinates, x => new HighlightBoundaryOutline(x.Mask, color, outlineThickness));
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

    private static string GetBoundaryOutlineColor(IHexHighlightType highlightType) =>
        highlightType.BoundaryOutlineColor;

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
    
    public IUnit? Attacker => CurrentState is WeaponsAttackState weaponsAttackState ? weaponsAttackState.Attacker : null;

    public void HandleHexSelection(Hex selectedHex)
    {
        CurrentState.HandleHexSelection(selectedHex);
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

        var segments = path.Segments.Select(p=> new PathSegmentViewModel(p)).ToList();
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
    public Func<Task<(byte[] PngBytes, int WidthPixels, int HeightPixels)>>? MapCaptureAsync { get; set; }

    public IAsyncCommand ExportMapToPdfCommand => field ??= new AsyncCommand(async () =>
    {
        if (MapCaptureAsync == null || _pdfExportService == null || _fileService == null) return;
        try
        {
            var (pngBytes, widthPixels, heightPixels) = await MapCaptureAsync();
            if (pngBytes.Length == 0) return;
            var widthPoints = widthPixels * 72 / 96;
            var heightPoints = heightPixels * 72 / 96;
            var pdfBytes = await _pdfExportService.GeneratePdfFromPngAsync(
                pngBytes, widthPoints, heightPoints);
            await _fileService.SaveBinaryFile(
                _localizationService.GetString("BattleMap_ExportPdfDialogTitle"),
                "map.pdf",
                pdfBytes,
                ".pdf",
                "PDF files");
        }
        catch (Exception)
        {
            // Silently ignore export failures (e.g. user cancelled save dialog)
        }
    });
    public ITerrainAssetService TerrainAssetService { get; }

    public ITerrainBitmaskService? TerrainBitmaskService { get; }

    public IScheduler Scheduler => _dispatcherService.Scheduler;

    private Task ProcessGameEnded(GameEndedCommand command)
    {
        IsGameOver = true;
        GameEndReason = command.Reason;
        NotifyStateChanged();
        return Task.CompletedTask;
    }

    public Task NavigateToEndGame()
    {
        // If the game ended with victory, navigate to the end game screen
        // For other reasons navigate back to a menu
        if (GameEndReason != GameEndReason.Victory || _game == null) return GoToMainMenu();
        var endGameViewModel = NavigationService.GetNewViewModel<EndGameViewModel>();
        if (endGameViewModel == null) return GoToMainMenu();
        // Initialize the end game view model with the game and reason
        endGameViewModel.Initialize(_game, GameEndReason);
        return NavigationService.NavigateToViewModelAsync(endGameViewModel);
    }
    
    public void Dispose()
    {
        HexConfiguration.PropertyChanged -= _hexConfigurationChangedHandler;
        _gameSubscription?.Dispose();
        _commandSubscription?.Dispose();
        if (Game is { IsDisposed: false })
        {
            Game.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    public override void DetachHandlers()
    {
        HexConfiguration.PropertyChanged -= _hexConfigurationChangedHandler;
        base.DetachHandlers();
        _gameSubscription?.Dispose();
        _commandSubscription?.Dispose();
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
