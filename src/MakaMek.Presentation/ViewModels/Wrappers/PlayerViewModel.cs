using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class PlayerViewModel : BindableBase
{
    private readonly Action? _onUnitChanged;
    private readonly Action<PlayerViewModel>? _joinGameAction;
    private readonly Action<PlayerViewModel>? _setReadyAction;
    private readonly Func<PlayerViewModel, Task>? _showAvailableUnits;
    private readonly Func<UnitData, PilotData?, bool, Task<PilotEditResult?>>? _showUnitInfo;
    private readonly Dictionary<Guid, PilotData> _unitPilots = new();
    private string _editableName;
    private readonly Func<Player, Task>? _onPlayerNameChanged;
    private readonly Func<bool> _isConnectionAvailable;
    private float _aggressivenessIndex;

    public Player Player
    {
        get;
    }

    public bool IsLocalPlayer { get; }

    /// <summary>
    /// Indicates whether this player can be removed from the game setup
    /// </summary>
    public bool IsRemovable => !field && IsLocalPlayer && Status == PlayerStatus.NotJoined;

    public PlayerStatus Status => Player.Status;

    public ObservableCollection<UnitViewModel> Units { get; }

    public bool IsBot => Player.ControlType == PlayerControlType.Bot;

    public float AggressivenessIndex
    {
        get => _aggressivenessIndex;
        set => SetProperty(ref _aggressivenessIndex, value);
    }

    public bool IsEditingName
    {
        get;
        set
        {
            SetProperty(ref field, value);
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
        NotifyPropertyChanged(nameof(CanEditAggressiveness));
    }

    public ICommand ShowAvailableUnitsCommand { get; }
    public ICommand ShowUnitInfoCommand { get; }
    public ICommand JoinGameCommand { get; }
    public ICommand SetReadyCommand { get; }
    public ICommand RemoveUnitCommand { get; }

    public string Name => Player.Name;

    public string Tint => Player.Tint;

    public PlayerViewModel(
        Player player,
        bool isLocalPlayer,
        Action<PlayerViewModel>? joinGameAction = null,
        Action<PlayerViewModel>? setReadyAction = null,
        Func<PlayerViewModel, Task>? showAvailableUnits = null,
        Func<UnitData, PilotData?, bool, Task<PilotEditResult?>>? showUnitInfo = null,
        Action? onUnitChanged = null,
        Func<Player, Task>? onPlayerNameChanged = null,
        bool isDefaultPlayer = false,
        Func<bool>? isConnectionAvailable = null)
    {
        Player = player;
        _editableName = player.Name;
        IsLocalPlayer = isLocalPlayer;
        IsRemovable = isDefaultPlayer;
        _isConnectionAvailable = isConnectionAvailable ?? (() => true);
        _joinGameAction = joinGameAction;
        _setReadyAction = setReadyAction;
        _showAvailableUnits = showAvailableUnits;
        _showUnitInfo = showUnitInfo;
        _onUnitChanged = onUnitChanged;
        _onPlayerNameChanged = onPlayerNameChanged;

        Units = [];
        //AddUnitCommand = new AsyncCommand();
        JoinGameCommand = new AsyncCommand(ExecuteJoinGame);
        SetReadyCommand = new AsyncCommand(ExecuteSetReady);
        ShowAvailableUnitsCommand = new AsyncCommand(ExecuteShowUnits);
        ShowUnitInfoCommand = new AsyncCommand<Guid>(ExecuteShowUnitInfo);
        RemoveUnitCommand = new AsyncCommand<Guid>(ExecuteRemoveUnit);
    }
    
    public bool CanJoin => IsLocalPlayer && Units.Count > 0 && Status == PlayerStatus.NotJoined && _isConnectionAvailable();
    public bool CanSetReady => IsLocalPlayer && Status == PlayerStatus.Joined;

    public bool CanAddUnit => IsLocalPlayer && Status == PlayerStatus.NotJoined;

    public bool CanRemoveUnit => IsLocalPlayer && Status == PlayerStatus.NotJoined;

    public bool CanSelectUnit => IsLocalPlayer && Status == PlayerStatus.NotJoined;

    public bool CanEditName => IsLocalPlayer && Status == PlayerStatus.NotJoined && !IsEditingName;

    public bool CanEditAggressiveness => IsBot && Status == PlayerStatus.NotJoined;

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
    
    private async Task ExecuteShowUnits()
    {
        if (!CanAddUnit) return;

        if (_showAvailableUnits != null)
        {
            await _showAvailableUnits.Invoke(this);
        }
    }

    private async Task ExecuteShowUnitInfo(Guid unitId)
    {
        if (unitId == Guid.Empty) return;

        var unit = Units.FirstOrDefault(u => u.Id == unitId);
        if (unit == null) return;

        var pilotData = GetPilotDataForUnit(unitId);
        if (_showUnitInfo != null)
        {
            var canEdit = CanEditName;
            var result = await _showUnitInfo.Invoke(unit.UnitData, pilotData, canEdit);
            if (result != null)
            {
                if (result.PilotData is { } editedPilot)
                {
                    UpdatePilotForUnit(unitId, editedPilot);
                }
                if (!string.IsNullOrWhiteSpace(result.UnitData.Name) &&
                    result.UnitData.Name != unit.UnitData.Name)
                {
                    unit.StartEditingName();
                    unit.EditableName = result.UnitData.Name;
                    unit.SaveName();
                }
            }
        }
    }

    private Task ExecuteRemoveUnit(Guid unitId)
    {
        if (!CanRemoveUnit || unitId == Guid.Empty) return Task.CompletedTask;

        var unit = Units.FirstOrDefault(u => u.Id == unitId);
        if (unit == null) return Task.CompletedTask;
        var removed = Units.Remove(unit);
        if (!removed) return Task.CompletedTask;
        
        // Remove pilot data for this unit
        _unitPilots.Remove(unitId);
        
        NotifyPropertyChanged(nameof(CanJoin));
        NotifyPropertyChanged(nameof(CanSetReady));
        _onUnitChanged?.Invoke();

        return Task.CompletedTask;
    }

    public Task AddUnit(UnitData unit, PilotData? pilotData = null)
    {
        if (unit.Id == null || unit.Id == Guid.Empty)
        {
            unit = unit with { Id = Guid.NewGuid() };
        }
        Units.Add(new UnitViewModel(unit));

        // Create a default pilot for this unit
        _unitPilots[unit.Id!.Value] = pilotData ?? PilotData.CreateDefaultPilot("MechWarrior","");

        NotifyPropertyChanged(nameof(CanJoin));
        _onUnitChanged?.Invoke();
        
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
            var pilotAssignment = pilotAssignments.FirstOrDefault(pa => pa.UnitId == unit.Id);
            
            AddUnit(unit, pilotAssignment.UnitId != Guid.Empty ? pilotAssignment.PilotData : null );
        }
    }

    public List<UnitData> GetUnitsData()
    {
        return Units.Select(u => u.UnitData).ToList();
    }

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

    /// <summary>
    /// Gets the bot settings for this player
    /// </summary>
    /// <returns>BotSettings with configured AggressivenessIndex, or Default for non-bots</returns>
    public BotSettings GetBotSettings()
    {
        if (!IsBot) return BotSettings.Default;
        return new BotSettings(_aggressivenessIndex);
    }
}