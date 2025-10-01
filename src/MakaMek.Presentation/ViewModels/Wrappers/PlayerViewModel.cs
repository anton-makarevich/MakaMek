using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class PlayerViewModel : BindableBase
{
    private UnitData? _selectedUnit;
    private readonly Action? _onUnitChanged;
    private readonly Action<PlayerViewModel>? _joinGameAction;
    private readonly Action<PlayerViewModel>? _setReadyAction;
    private readonly Dictionary<Guid, PilotData> _unitPilots = new();
    private bool _isEditingName;
    private string _editableName;
    private readonly Func<Player, Task>? _onPlayerNameChanged;
    private bool _isTableVisible;

    public Player Player
    {
        get;
    }

    public bool IsLocalPlayer { get; }

    public PlayerStatus Status => Player.Status;

    public ObservableCollection<UnitData> Units { get; }
    public ObservableCollection<UnitData> AvailableUnits { get; }

    /// <summary>
    /// Gets the ViewModel for the available units table
    /// </summary>
    public AvailableUnitsTableViewModel AvailableUnitsTableViewModel { get; }

    public UnitData? SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            SetProperty(ref _selectedUnit, value);
            NotifyPropertyChanged(nameof(CanAddUnit));
        }
    }

    public bool IsEditingName
    {
        get => _isEditingName;
        set
        {
            SetProperty(ref _isEditingName, value);
            NotifyPropertyChanged(nameof(CanEditName));
        }
    }

    public string EditableName
    {
        get => _editableName;
        set => SetProperty(ref _editableName, value);
    }

    /// <summary>
    /// Gets or sets whether the available units table is visible
    /// </summary>
    public bool IsTableVisible
    {
        get => _isTableVisible;
        set => SetProperty(ref _isTableVisible, value);
    }

    public void RefreshStatus()
    {
        NotifyPropertyChanged(nameof(Status));
        NotifyPropertyChanged(nameof(CanAddUnit));
        NotifyPropertyChanged(nameof(CanJoin));
        NotifyPropertyChanged(nameof(CanSetReady));
        NotifyPropertyChanged(nameof(CanSelectUnit));
        NotifyPropertyChanged(nameof(CanEditName));
    }

    public ICommand AddUnitCommand { get; }
    public ICommand JoinGameCommand { get; }
    public ICommand SetReadyCommand { get; }
    public ICommand ShowTableCommand { get; }
    public ICommand HideTableCommand { get; }

    public string Name => Player.Name;
    
    public PlayerViewModel(
        Player player,
        bool isLocalPlayer,
        IEnumerable<UnitData> availableUnits,
        Action<PlayerViewModel>? joinGameAction = null,
        Action<PlayerViewModel>? setReadyAction = null,
        Action? onUnitChanged = null,
        Func<Player, Task>? onPlayerNameChanged = null)
    {
        Player = player;
        _editableName = player.Name;
        IsLocalPlayer = isLocalPlayer;
        _joinGameAction = joinGameAction;
        _setReadyAction = setReadyAction;
        _onUnitChanged = onUnitChanged;
        _onPlayerNameChanged = onPlayerNameChanged;

        Units = [];
        AvailableUnits = new ObservableCollection<UnitData>(availableUnits);
        AddUnitCommand = new AsyncCommand(AddUnit);
        JoinGameCommand = new AsyncCommand(ExecuteJoinGame);
        SetReadyCommand = new AsyncCommand(ExecuteSetReady);
        ShowTableCommand = new AsyncCommand(ShowTable);
        HideTableCommand = new AsyncCommand(HideTable);

        // Initialize the AvailableUnitsTableViewModel
        AvailableUnitsTableViewModel = new AvailableUnitsTableViewModel(
            AvailableUnits,
            AddUnitCommand,
            () => CanAddUnit,
            HideTableCommand);
    }

    private Task ExecuteJoinGame()
    {
        if (!CanJoin) return Task.CompletedTask;
        
        _joinGameAction?.Invoke(this); 
        
        return Task.CompletedTask;
    }
    
    private Task ExecuteSetReady()
    {
        if (!CanSetReady) return Task.CompletedTask;
        
        _setReadyAction?.Invoke(this);
        
        return Task.CompletedTask;
    }
    
    public bool CanAddUnit => IsLocalPlayer && AvailableUnitsTableViewModel.SelectedUnit != null && Status == PlayerStatus.NotJoined;

    public bool CanSelectUnit => IsLocalPlayer && Status == PlayerStatus.NotJoined;

    public bool CanEditName => IsLocalPlayer && Status == PlayerStatus.NotJoined && !IsEditingName;

    private Task ShowTable()
    {
        IsTableVisible = true;
        return Task.CompletedTask;
    }

    private Task HideTable()
    {
        IsTableVisible = false;
        return Task.CompletedTask;
    }

    private Task AddUnit()
    {
        if (!CanAddUnit) return Task.CompletedTask;

        // Get the selected unit from the table ViewModel
        var selectedUnit = AvailableUnitsTableViewModel.SelectedUnit;
        if (!selectedUnit.HasValue) return Task.CompletedTask;

        var unit = selectedUnit.Value;
        var unitId = Guid.NewGuid();
        unit.Id = unitId;
        Units.Add(unit);

        // Create a default pilot for this unit
        _unitPilots[unitId] = PilotData.CreateDefaultPilot("MechWarrior","");

        NotifyPropertyChanged(nameof(CanJoin));
        _onUnitChanged?.Invoke();

        // Clear selection in the table ViewModel
        AvailableUnitsTableViewModel.ClearSelection();

        // Hide the table after adding a unit
        IsTableVisible = false;

        SelectedUnit = null;
        (AddUnitCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        return Task.CompletedTask;
    }

    public void StartEditingName()
    {
        if (!CanEditName) return;

        IsEditingName = true;
        EditableName = Player.Name;
    }

    public void SaveName()
    {
        if (string.IsNullOrWhiteSpace(EditableName))
        {
            // Don't allow empty names
            EditableName = Player.Name;
            IsEditingName = false;
            return;
        }

        Player.Name = EditableName.Trim();
        IsEditingName = false;
        
        NotifyPropertyChanged(nameof(Name));
        NotifyPropertyChanged(nameof(EditableName));

        // Notify parent that the player name changed
        _onPlayerNameChanged?.Invoke(Player);
    }

    public void CancelEditName()
    {
        EditableName = Player.Name;
        IsEditingName = false;
    }

    public void AddUnits(IEnumerable<UnitData> unitsToAdd, List<PilotAssignmentData> pilotAssignments)
    {
        Units.Clear();
        _unitPilots.Clear();
        foreach(var unit in unitsToAdd)
        {
            var unitToAdd = unit;
            if (unitToAdd.Id == Guid.Empty) unitToAdd.Id = Guid.NewGuid();
            Units.Add(unitToAdd);

            var pilotAssignment = pilotAssignments.FirstOrDefault(pa => pa.UnitId == unit.Id);
            if (pilotAssignment.UnitId != Guid.Empty)
            {
                _unitPilots[unit.Id!.Value] = pilotAssignment.PilotData;
            }
            else if (unitToAdd.Id != null)
            {
                _unitPilots[unitToAdd.Id.Value] = PilotData.CreateDefaultPilot("MechWarrior","");
            }
        }
        _onUnitChanged?.Invoke();
    }
    public bool CanJoin => IsLocalPlayer && Units.Count > 0 && Status == PlayerStatus.NotJoined;
    public bool CanSetReady => IsLocalPlayer && Status == PlayerStatus.Joined;

    /// <summary>
    /// Gets the pilot data for the specified unit
    /// </summary>
    /// <param name="unitId">The ID of the unit</param>
    /// <returns>The pilot data for the unit</returns>
    public PilotData? GetPilotDataForUnit(Guid unitId)
    {
        if (_unitPilots.TryGetValue(unitId, out var pilotData)) return pilotData;
        return null;
    }

    /// <summary>
    /// Updates the pilot data for the specified unit
    /// </summary>
    /// <param name="unitId">The ID of the unit</param>
    /// <param name="pilotData">The new pilot data</param>
    public void UpdatePilotForUnit(Guid unitId, PilotData pilotData)
    {
        _unitPilots[unitId] = pilotData;
    }
}