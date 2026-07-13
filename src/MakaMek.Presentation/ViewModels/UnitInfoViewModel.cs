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
            Pilot = new PilotViewModel(pilot);
        }

        CloseCommand = new AsyncCommand(Close);
        SaveCommand = new AsyncCommand(Save);

        if (canEdit)
        {
            Pilot?.StartEditing();
        }

        EditableName = DisplayName;
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

    public string EditableName
    {
        get;
        set => SetProperty(ref field, value);
    }

    public Task<PilotEditResult?> GetResultAsync()
    {
        return _resultTaskCompletionSource.Task;
    }

    private Task Save()
    {
        var editedPilot = CanEdit ? Pilot?.SaveEdit() : null;

        if (CanEdit && !string.IsNullOrWhiteSpace(EditableName) && EditableName.Trim() != DisplayName)
        {
            _originalUnitData = _originalUnitData with { Name = EditableName.Trim() };
        }

        var result = new PilotEditResult(_originalUnitData, editedPilot);
        _resultTaskCompletionSource.TrySetResult(result);
        return Task.CompletedTask;
    }

    private Task Close()
    {
        _resultTaskCompletionSource.TrySetResult(null);
        return Task.CompletedTask;
    }
}
