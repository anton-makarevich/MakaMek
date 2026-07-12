using Sanet.MakaMek.Core.Data.Units;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class PilotViewModel : BindableBase
{
    private const int MinSkill = 0;
    private const int MaxSkill = 8;

    private PilotData _pilotData;
    private string _editableFirstName = string.Empty;
    private string _editableLastName = string.Empty;
    private int _editableGunnery;
    private int _editablePiloting;

    public PilotViewModel(PilotData pilotData)
    {
        _pilotData = pilotData;
    }

    public Guid Id => _pilotData.Id;

    public string FirstName => _pilotData.FirstName;

    public string LastName => _pilotData.LastName;

    public string FullName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : $"{FirstName} {LastName}";

    public int Gunnery => _pilotData.Gunnery;

    public int Piloting => _pilotData.Piloting;

    public int Health => _pilotData.Health;

    public int Injuries => _pilotData.Injuries;

    public bool IsConscious => _pilotData.IsConscious;

    public bool IsDead => Injuries >= Health;

    public int? UnconsciousInTurn => _pilotData.UnconsciousInTurn;

    public string EditableFirstName
    {
        get => _editableFirstName;
        set => SetProperty(ref _editableFirstName, value);
    }

    public string EditableLastName
    {
        get => _editableLastName;
        set => SetProperty(ref _editableLastName, value);
    }

    public int EditableGunnery
    {
        get => _editableGunnery;
        set => SetProperty(ref _editableGunnery, value);
    }

    public int EditablePiloting
    {
        get => _editablePiloting;
        set => SetProperty(ref _editablePiloting, value);
    }

    public PilotData PilotData => _pilotData;

    public void StartEditing()
    {
        EditableFirstName = FirstName;
        EditableLastName = LastName;
        EditableGunnery = Gunnery;
        EditablePiloting = Piloting;
    }

    public PilotData SaveEdit()
    {
        var firstName = string.IsNullOrWhiteSpace(EditableFirstName)
            ? "MechWarrior"
            : EditableFirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(EditableLastName)
            ? string.Empty
            : EditableLastName.Trim();

        var gunnery = Math.Clamp(EditableGunnery, MinSkill, MaxSkill);
        var piloting = Math.Clamp(EditablePiloting, MinSkill, MaxSkill);

        _pilotData = _pilotData with
        {
            FirstName = firstName,
            LastName = lastName,
            Gunnery = gunnery,
            Piloting = piloting
        };

        NotifyPropertyChanged(nameof(FirstName));
        NotifyPropertyChanged(nameof(LastName));
        NotifyPropertyChanged(nameof(FullName));
        NotifyPropertyChanged(nameof(Gunnery));
        NotifyPropertyChanged(nameof(Piloting));
        NotifyPropertyChanged(nameof(PilotData));

        return _pilotData;
    }

    public void CancelEdit()
    {
        EditableFirstName = FirstName;
        EditableLastName = LastName;
        EditableGunnery = Gunnery;
        EditablePiloting = Piloting;
    }
}
