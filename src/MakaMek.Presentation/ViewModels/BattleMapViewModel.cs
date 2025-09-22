using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Client;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Phases;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Presentation.UiStates;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class BattleMapViewModel : BaseViewModel
{
    private ClientGame? _game;
    private IDisposable? _gameSubscription;
    private IDisposable? _commandSubscription;
    private List<Unit> _unitsToDeploy = [];
    private Unit? _selectedUnit;
    private readonly ObservableCollection<string> _commandLog = [];
    private bool _isCommandLogExpanded;
    private bool _isRecordSheetExpanded;
    private readonly ILocalizationService _localizationService;
    private HexCoordinates? _directionSelectorPosition;
    private bool _isDirectionSelectorVisible;
    private IEnumerable<HexDirection>? _availableDirections;
    private List<PathSegmentViewModel>? _movementPath;
    private List<WeaponAttackViewModel>? _weaponAttacks;
    private bool _isWeaponSelectionVisible;
    private readonly IDispatcherService _dispatcherService;
    private List<UiEventViewModel> _selectedUnitEvents = [];
    private AimedShotLocationSelectorViewModel? _unitPartSelector;
    private bool _isUnitPartSelectorVisible;


    public HexCoordinates? DirectionSelectorPosition
    {
        get => _directionSelectorPosition;
        private set => SetProperty(ref _directionSelectorPosition, value);
    }

    public bool IsDirectionSelectorVisible
    {
        get => _isDirectionSelectorVisible;
        private set => SetProperty(ref _isDirectionSelectorVisible, value);
    }

    public IEnumerable<HexDirection>? AvailableDirections
    {
        get => _availableDirections;
        private set => SetProperty(ref _availableDirections, value);
    }

    public List<PathSegmentViewModel>? MovementPath
    {
        get => _movementPath;
        private set => SetProperty(ref _movementPath, value);
    }

    public List<WeaponAttackViewModel>? WeaponAttacks
    {
        get => _weaponAttacks;
        private set => SetProperty(ref _weaponAttacks, value);
    }

    public AimedShotLocationSelectorViewModel? UnitPartSelector
    {
        get => _unitPartSelector;
        private set => SetProperty(ref _unitPartSelector, value);
    }

    public bool IsUnitPartSelectorVisible
    {
        get => _isUnitPartSelectorVisible;
        private set => SetProperty(ref _isUnitPartSelectorVisible, value);
    }

    public void DirectionSelectedCommand(HexDirection direction)
    {
        CurrentState.HandleFacingSelection(direction);
    }

    public BattleMapViewModel(IImageService imageService, ILocalizationService localizationService, IDispatcherService dispatcherService)
    {
        ImageService = imageService;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;
        CurrentState = new IdleState();
        HideBodyPartSelectorCommand = new AsyncCommand(() =>
        {
            HideAimedShotLocationSelector();
            return Task.CompletedTask;
        });
    }

    public ClientGame? Game
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

    public ObservableCollection<WeaponSelectionViewModel> WeaponSelectionItems { get; } = [];

    public bool IsWeaponSelectionVisible
    {
        get => CurrentState is WeaponsAttackState { CurrentStep: WeaponsAttackStep.TargetSelection, SelectedTarget: not null } 
            && _isWeaponSelectionVisible;
        set => SetProperty(ref _isWeaponSelectionVisible, value);
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
            .Subscribe( ProcessCommand );
        
        _gameSubscription = Game.TurnChanges
            .StartWith(Game.Turn)
            .CombineLatest<int, PhaseNames, IPlayer?, int, (int Turn, PhaseNames Phase, IPlayer? Player, int UnitsToPlay)>(Game.PhaseChanges.StartWith(Game.TurnPhase),
                Game.ActivePlayerChanges.StartWith(Game.ActivePlayer),
                Game.UnitsToPlayChanges.StartWith(Game.UnitsToPlayCurrentStep),
                (turn, phase, player, units) => (turn, phase, player, units))
            .Subscribe(_ =>
            {
                ClearSelection();
                UpdateGamePhase();
                NotifyStateChanged();
            });
    }

    private void ProcessCommand(IGameCommand command)
    {
        _dispatcherService.RunOnUIThread(() =>
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
                    ProcessMechStandUp(standUpCommand);
                    break;
            }
        });
    }

    private void ProcessMechStandUp(MechStandUpCommand standUpCommand)
    {
        if (CurrentState is MovementState movementState 
            && standUpCommand.UnitId == SelectedUnit?.Id)
        {
            movementState.ResumeMovementAfterStandup();
        }
    }

    private void ProcessWeaponAttackDeclaration(WeaponAttackDeclarationCommand command)
    {
        if (Game == null) return;
    
        var attacker = Game.Players
            .SelectMany(p => p.Units)
            .FirstOrDefault(u => u.Id == command.AttackerId);
    
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
        if (Game is not { ActivePlayer: not null } clientGame)
        {
            return;
        }

        switch (TurnPhaseName)
        {
            case PhaseNames.Deployment when clientGame.ActivePlayer.Units.Any(u => !u.IsDeployed):
                TransitionToState(new DeploymentState(this));
                ShowUnitsToDeploy();
                break;
        
            case PhaseNames.Movement when clientGame.UnitsToPlayCurrentStep > 0:
                TransitionToState(new MovementState(this));
                break;
        
            case PhaseNames.WeaponsAttack when clientGame.UnitsToPlayCurrentStep > 0:
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
        if (Game?.ActivePlayer == null || Game?.UnitsToPlayCurrentStep < 1)
        {
            UnitsToDeploy = [];
            return;
        }
        UnitsToDeploy = Game?.ActivePlayer?.Units.Where(u => !u.IsDeployed).ToList()??[];
    }

    private void TransitionToState(IUiState newState)
    {
        CurrentState = newState;
        NotifyStateChanged();
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
    }

    internal void HighlightHexes(List<HexCoordinates> coordinates, bool isHighlighted)
    {
        var hexesToHighlight = Game?.BattleMap?.GetHexes().Where(h => coordinates.Contains(h.Coordinates)).ToList();
        if (hexesToHighlight == null) return;
        foreach (var hex in hexesToHighlight)
        {
            hex.IsHighlighted = isHighlighted;
        }
    }

    public List<Unit> UnitsToDeploy
    {
        get => _unitsToDeploy;
        private set
        {
            SetProperty(ref _unitsToDeploy, value);
            NotifyPropertyChanged(nameof(AreUnitsToDeployVisible));
        }
    }

    public bool AreUnitsToDeployVisible => Game is not null
                                           && Game.CanActivePlayerAct
                                           && CurrentState is DeploymentState
                                           && UnitsToDeploy.Count > 0
                                           && SelectedUnit == null;

    public int Turn => Game?.Turn ?? 0;

    public PhaseNames TurnPhaseName => Game?.TurnPhase ?? PhaseNames.Start;
    
    public string ActivePlayerName => Game?.ActivePlayer?.Name ?? string.Empty;

    public string ActivePlayerTint => Game?.ActivePlayer?.Tint ?? "#FFFFFF";

    public IImageService ImageService { get; }

    public Unit? SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            if (value == _selectedUnit) return;
            SetProperty(ref _selectedUnit, value);
            CurrentState.HandleUnitSelection(value);
            NotifyPropertyChanged(nameof(AreUnitsToDeployVisible));
            NotifyPropertyChanged(nameof(IsRecordSheetButtonVisible));
            NotifyPropertyChanged(nameof(IsRecordSheetPanelVisible));
            
            UpdateSelectedUnitEvents();
        }
    }
    
    public Unit? Attacker => CurrentState is WeaponsAttackState weaponsAttackState ? weaponsAttackState.Attacker : null;

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

    public bool IsCommandLogExpanded
    {
        get => _isCommandLogExpanded;
        set => SetProperty(ref _isCommandLogExpanded, value);
    }

    public bool IsRecordSheetExpanded
    {
        get => _isRecordSheetExpanded;
        set
        {
            SetProperty(ref _isRecordSheetExpanded, value); 
            NotifyPropertyChanged(nameof(IsRecordSheetButtonVisible));
            NotifyPropertyChanged(nameof(IsRecordSheetPanelVisible));
        }
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

    public IEnumerable<Unit> Units => Game?.AlivePlayers.SelectMany(p => p.AliveUnits) ?? [];

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

    public void ShowMovementPath(List<PathSegment> path)
    {
        HideMovementPath();
        if (path.Count < 1)
        {
            return;
        }

        var segments = path.Select(p=> new PathSegmentViewModel(p)).ToList();
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
        if (_selectedUnit == null)
        {
            _selectedUnitEvents = [];
        }
        else
        {
            _selectedUnitEvents = _selectedUnit.Events
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
}
