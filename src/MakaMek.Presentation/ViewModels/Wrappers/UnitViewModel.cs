using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Localization;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class UnitViewModel : BindableBase
{
    private UnitData _unitData;
    private PilotData? _pilotData;
    private readonly ILocalizationService? _localizationService;
    private bool _isEditingName;
    private string _editableName = string.Empty;

    public UnitViewModel(UnitData unitData, PilotData? pilotData = null, ILocalizationService? localizationService = null)
    {
        _unitData = unitData;
        _pilotData = pilotData;
        _localizationService = localizationService;
    }

    public Guid Id => _unitData.Id ?? Guid.Empty;
    public string Model => _unitData.Model;
    public string Chassis => _unitData.Chassis;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_unitData.Name))
                return _unitData.Name;
            return $"{_unitData.Chassis} {_unitData.Model}";
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

    public bool CanEditName => !IsEditingName;

    public UnitData UnitData => _unitData;

    public string PilotName => _pilotData is { } pilot
        ? $"{pilot.FirstName} {pilot.LastName}".Trim()
        : _localizationService?.GetString("UnitItem_NoPilot") ?? "No Pilot";

    public void UpdatePilot(PilotData pilotData)
    {
        _pilotData = pilotData;
        NotifyPropertyChanged(nameof(PilotName));
    }

    public void UpdateUnitData(UnitData unitData)
    {
        _unitData = unitData;
        NotifyPropertyChanged(nameof(DisplayName));
        NotifyPropertyChanged(nameof(UnitData));
    }

    public void StartEditingName()
    {
        if (!CanEditName) return;

        IsEditingName = true;
        EditableName = _unitData.Name ?? string.Empty;
    }

    public void SaveName()
    {
        if (!IsEditingName) return;

        if (string.IsNullOrWhiteSpace(EditableName))
        {
            _unitData = _unitData with { Name = null };
        }
        else
        {
            _unitData = _unitData with { Name = EditableName.Trim() };
        }

        IsEditingName = false;
        NotifyPropertyChanged(nameof(DisplayName));
        NotifyPropertyChanged(nameof(UnitData));
    }

    public void CancelEditName()
    {
        EditableName = DisplayName;
        IsEditingName = false;
    }
}
