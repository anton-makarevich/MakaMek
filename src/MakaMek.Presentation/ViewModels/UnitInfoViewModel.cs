using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class UnitInfoViewModel : BaseViewModel, IResultProvider<PilotEditResult?>
{
    private readonly TaskCompletionSource<PilotEditResult?> _resultTaskCompletionSource = new();
    private UnitData _originalUnitData;
    private bool _isEditingName;
    private string _editableName = string.Empty;

    public Unit Unit { get; }
    public bool HasPilot { get; }
    public bool CanEdit { get; }
    public PilotViewModel? Pilot { get; }

    public ICommand CloseCommand { get; }
    public ICommand SaveCommand { get; }

    public UnitInfoViewModel(UnitData unitData, PilotData? pilotData, IMechFactory mechFactory,
        bool canEdit = false)
    {
        _originalUnitData = unitData;
        CanEdit = canEdit;

        Unit = mechFactory.Create(unitData);

        if (pilotData.HasValue)
        {
            var pilot = new MechWarrior(pilotData.Value);
            Unit.AssignPilot(pilot);
            HasPilot = true;
            Pilot = new PilotViewModel(pilotData.Value);
        }

        CloseCommand = new AsyncCommand(Close);
        SaveCommand = new AsyncCommand(Save);

        if (canEdit)
        {
            Pilot?.StartEditing();
        }
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_originalUnitData.Name))
                return _originalUnitData.Name;
            return $"{_originalUnitData.Chassis} {_originalUnitData.Model}";
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

    public bool CanEditName => !_isEditingName;

    public string EditableName
    {
        get => _editableName;
        set => SetProperty(ref _editableName, value);
    }

    public void StartEditingName()
    {
        if (!CanEditName || !CanEdit) return;

        IsEditingName = true;
        EditableName = _originalUnitData.Name ?? string.Empty;
    }

    public void SaveName()
    {
        if (!IsEditingName) return;

        if (!string.IsNullOrWhiteSpace(EditableName))
        {
            _originalUnitData = _originalUnitData with { Name = EditableName.Trim() };
        }

        IsEditingName = false;
        NotifyPropertyChanged(nameof(DisplayName));
    }

    public void CancelEditName()
    {
        EditableName = DisplayName;
        IsEditingName = false;
    }

    public Task<PilotEditResult?> GetResultAsync()
    {
        return _resultTaskCompletionSource.Task;
    }

    private Task Save()
    {
        var editedPilot = Pilot?.SaveEdit();
        var result = new PilotEditResult(_originalUnitData, editedPilot ?? default);
        _resultTaskCompletionSource.TrySetResult(result);
        return CloseAsync();
    }

    private Task Close()
    {
        _resultTaskCompletionSource.TrySetResult(null);
        return Task.CompletedTask;
    }
}
