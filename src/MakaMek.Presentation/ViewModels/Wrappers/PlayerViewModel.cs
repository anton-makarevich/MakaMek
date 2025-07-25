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

    public Player Player { get; }
    public bool IsLocalPlayer { get; }
    
    public PlayerStatus Status => Player.Status;

    public ObservableCollection<UnitData> Units { get; }
    public ObservableCollection<UnitData> AvailableUnits { get; }
    
    public UnitData? SelectedUnit
    {
        get => _selectedUnit;
        set
        {
            SetProperty(ref _selectedUnit, value); 
            NotifyPropertyChanged(nameof(CanAddUnit));
        }
    }

    public void RefreshStatus()
    {
        NotifyPropertyChanged(nameof(Status));
        NotifyPropertyChanged(nameof(CanAddUnit));
        NotifyPropertyChanged(nameof(CanJoin));
        NotifyPropertyChanged(nameof(CanSetReady));
        NotifyPropertyChanged(nameof(CanSelectUnit));
    }

    public ICommand AddUnitCommand { get; }
    public ICommand JoinGameCommand { get; }
    public ICommand SetReadyCommand { get; }

    public string Name => Player.Name;
    
    public PlayerViewModel(
        Player player, 
        bool isLocalPlayer, 
        IEnumerable<UnitData> availableUnits, 
        Action<PlayerViewModel>? joinGameAction = null, 
        Action<PlayerViewModel>? setReadyAction = null,
        Action? onUnitChanged = null) 
    {
        Player = player;
        IsLocalPlayer = isLocalPlayer;
        _joinGameAction = joinGameAction;
        _setReadyAction = setReadyAction;
        _onUnitChanged = onUnitChanged;
        
        Units = [];
        AvailableUnits = new ObservableCollection<UnitData>(availableUnits);
        AddUnitCommand = new AsyncCommand(AddUnit);
        JoinGameCommand = new AsyncCommand(ExecuteJoinGame);
        SetReadyCommand = new AsyncCommand(ExecuteSetReady);
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
    
    public bool CanAddUnit => IsLocalPlayer && SelectedUnit != null && Status == PlayerStatus.NotJoined;
    
    public bool CanSelectUnit => IsLocalPlayer && Status == PlayerStatus.NotJoined;

    private Task AddUnit()
    {
        if (!CanAddUnit) return Task.CompletedTask;

        var unit = SelectedUnit!.Value;
        var unitId = Guid.NewGuid();
        unit.Id = unitId;
        Units.Add(unit);

        // Create a default pilot for this unit
        _unitPilots[unitId] = PilotData.CreateDefaultPilot("MechWarrior","");

        NotifyPropertyChanged(nameof(CanJoin));
        _onUnitChanged?.Invoke();
        SelectedUnit = null;
        (AddUnitCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        return Task.CompletedTask;
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