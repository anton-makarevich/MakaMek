using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class PlayerViewModel : BindableBase
{
    private readonly Action? _onUnitChanged;
    private readonly Action<PlayerViewModel>? _joinGameAction;
    private readonly Action<PlayerViewModel>? _setReadyAction;
    private readonly Action<PlayerViewModel>? _showAvailableUnits;
    private readonly Action<UnitData>? _removeUnitAction;
    private readonly Dictionary<Guid, PilotData> _unitPilots = new();
    private bool _isEditingName;
    private string _editableName;
    private readonly Func<Player, Task>? _onPlayerNameChanged;
    private readonly bool _isDefaultPlayer;

    public Player Player
    {
        get;
    }

    public bool IsLocalPlayer { get; }

    /// <summary>
    /// Indicates whether this player can be removed from the game setup
    /// </summary>
    public bool IsRemovable => !_isDefaultPlayer && Status == PlayerStatus.NotJoined;

    public PlayerStatus Status => Player.Status;

    public ObservableCollection<UnitData> Units { get; }

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

    public void RefreshStatus()
    {
        NotifyPropertyChanged(nameof(Status));
        NotifyPropertyChanged(nameof(CanAddUnit));
        NotifyPropertyChanged(nameof(CanRemoveUnit));
        NotifyPropertyChanged(nameof(CanJoin));
        NotifyPropertyChanged(nameof(CanSetReady));
        NotifyPropertyChanged(nameof(CanSelectUnit));
        NotifyPropertyChanged(nameof(CanEditName));
        NotifyPropertyChanged(nameof(IsRemovable));
    }

    public ICommand ShowAvailableUnitsCommand { get; }
    public ICommand JoinGameCommand { get; }
    public ICommand SetReadyCommand { get; }
    public ICommand RemoveUnitCommand { get; }

    public string Name => Player.Name;

    public PlayerViewModel(
        Player player,
        bool isLocalPlayer,
        Action<PlayerViewModel>? joinGameAction = null,
        Action<PlayerViewModel>? setReadyAction = null,
        Action<PlayerViewModel>? showAvailableUnits = null,
        Action? onUnitChanged = null,
        Func<Player, Task>? onPlayerNameChanged = null,
        Action<UnitData>? removeUnitAction = null,
        bool isDefaultPlayer = false)
    {
        Player = player;
        _editableName = player.Name;
        IsLocalPlayer = isLocalPlayer;
        _isDefaultPlayer = isDefaultPlayer;
        _joinGameAction = joinGameAction;
        _setReadyAction = setReadyAction;
        _showAvailableUnits = showAvailableUnits;
        _onUnitChanged = onUnitChanged;
        _onPlayerNameChanged = onPlayerNameChanged;
        _removeUnitAction = removeUnitAction;

        Units = [];
        //AddUnitCommand = new AsyncCommand();
        JoinGameCommand = new AsyncCommand(ExecuteJoinGame);
        SetReadyCommand = new AsyncCommand(ExecuteSetReady);
        ShowAvailableUnitsCommand = new AsyncCommand(ExecuteShowUnits);
        RemoveUnitCommand = new AsyncCommand<Guid>(ExecuteRemoveUnit);
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
    
    private Task ExecuteShowUnits()
    {
        if (!CanAddUnit) return Task.CompletedTask;

        _showAvailableUnits?.Invoke(this);

        return Task.CompletedTask;
    }

    private Task ExecuteRemoveUnit(Guid unitId)
    {
        if (!CanRemoveUnit) return Task.CompletedTask;

        var unit = Units.FirstOrDefault(u => u.Id == unitId);
        Units.Remove(unit);
        if (!unit.Id.HasValue || !unit.Id.HasValue) return Task.CompletedTask;
        
        // Remove pilot data for this unit
        _unitPilots.Remove(unitId);
        
        NotifyPropertyChanged(nameof(CanJoin));
        _onUnitChanged?.Invoke();
        _removeUnitAction?.Invoke(unit);

        return Task.CompletedTask;
    }

    public bool CanAddUnit => IsLocalPlayer && Status == PlayerStatus.NotJoined;

    public bool CanRemoveUnit => IsLocalPlayer && Status == PlayerStatus.NotJoined;

    public bool CanSelectUnit => IsLocalPlayer && Status == PlayerStatus.NotJoined;

    public bool CanEditName => IsLocalPlayer && Status == PlayerStatus.NotJoined && !IsEditingName;

    public Task AddUnit(UnitData unit)
    {
        if (!CanAddUnit) return Task.CompletedTask;
        
        var unitId = Guid.NewGuid();
        unit.Id = unitId;
        Units.Add(unit);

        // Create a default pilot for this unit
        _unitPilots[unitId] = PilotData.CreateDefaultPilot("MechWarrior","");

        NotifyPropertyChanged(nameof(CanJoin));
        _onUnitChanged?.Invoke();
        
        (ShowAvailableUnitsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
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