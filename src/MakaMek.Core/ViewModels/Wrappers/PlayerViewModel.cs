using Sanet.MVVM.Core.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Core.ViewModels.Wrappers;

public class PlayerViewModel : BindableBase
{
    private UnitData? _selectedUnit;
    private readonly Action? _onUnitChanged; 
    private readonly Action<PlayerViewModel>? _joinGameAction;

    public Player Player { get; }
    public bool IsLocalPlayer { get; }
    
    public PlayerStatus Status
    {
        get => Player.Status;
        set
        {
            Player.Status = value;
            NotifyPropertyChanged(nameof(CanJoin));
        }
    }

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

    public ICommand AddUnitCommand { get; }
    public ICommand JoinGameCommand { get; }

    public string Name => Player.Name;
    
    public PlayerViewModel(
        Player player, 
        bool isLocalPlayer, 
        IEnumerable<UnitData> availableUnits, 
        Action<PlayerViewModel>? joinGameAction = null, 
        Action? onUnitChanged = null) 
    {
        Player = player;
        IsLocalPlayer = isLocalPlayer;
        _joinGameAction = joinGameAction;
        _onUnitChanged = onUnitChanged;
        
        Units = [];
        AvailableUnits = new ObservableCollection<UnitData>(availableUnits);
        AddUnitCommand = new AsyncCommand(AddUnit);
        JoinGameCommand = new AsyncCommand(ExecuteJoinGame);
    }

    private Task ExecuteJoinGame()
    {
        if (!IsLocalPlayer) return Task.CompletedTask;
        
        Status = PlayerStatus.JoinRequested;
        _joinGameAction?.Invoke(this); 
        
        return Task.CompletedTask;
    }
    
    public bool CanAddUnit => IsLocalPlayer && SelectedUnit != null;

    private Task AddUnit()
    {
        if (!CanAddUnit) return Task.CompletedTask;
        
        var unit = SelectedUnit!.Value; 
        unit.Id = Guid.NewGuid(); 
        Units.Add(unit);
        NotifyPropertyChanged(nameof(CanJoin));
        _onUnitChanged?.Invoke();
        SelectedUnit = null; 
        (AddUnitCommand as AsyncCommand)?.RaiseCanExecuteChanged(); 
        return Task.CompletedTask;
    }

    public void AddUnits(IEnumerable<UnitData> unitsToAdd)
    {
        Units.Clear(); 
        foreach(var unit in unitsToAdd)
        {
            var unitToAdd = unit; 
            if (unitToAdd.Id == Guid.Empty) unitToAdd.Id = Guid.NewGuid(); 
            Units.Add(unitToAdd);
        }
        _onUnitChanged?.Invoke();
    }
    
    public bool CanSelectUnits => IsLocalPlayer; 
    public bool ShowAddUnitControls => IsLocalPlayer; 
    public bool ShowUnitListReadOnly => !IsLocalPlayer; 
    public bool CanJoin => IsLocalPlayer && Units.Count > 0 && Status != PlayerStatus.Joined;
}