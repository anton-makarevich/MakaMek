using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels.Wrappers;

public class PilotViewModel : BindableBase
{
    private const int MinSkill = 0;
    private const int MaxSkill = 8;

    private readonly IPilot _pilot;

    public PilotViewModel(IPilot pilot)
    {
        _pilot = pilot;
    }

    public IPilot Pilot => _pilot;

    public Guid Id => _pilot.Id;

    public string FirstName => _pilot.FirstName;

    public string LastName => _pilot.LastName;

    public string FullName => string.IsNullOrWhiteSpace(LastName)
        ? FirstName
        : $"{FirstName} {LastName}";

    public int Gunnery => _pilot.Gunnery;

    public int Piloting => _pilot.Piloting;

    public int Health => _pilot.Health;

    public int Injuries => _pilot.Injuries;

    public bool IsConscious => _pilot.IsConscious;

    public bool IsDead => _pilot.IsDead;

    public int? UnconsciousInTurn => _pilot.UnconsciousInTurn;

    public string EditableFirstName
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string EditableLastName
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public int EditableGunnery
    {
        get;
        set => SetProperty(ref field, value);
    }

    public int EditablePiloting
    {
        get;
        set => SetProperty(ref field, value);
    }

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

        return new PilotData
        {
            Id = _pilot.Id,
            FirstName = firstName,
            LastName = lastName,
            Gunnery = gunnery,
            Piloting = piloting,
            Health = _pilot.Health,
            Injuries = _pilot.Injuries,
            IsConscious = _pilot.IsConscious,
            UnconsciousInTurn = _pilot.UnconsciousInTurn
        };
    }

    public void CancelEdit()
    {
        EditableFirstName = FirstName;
        EditableLastName = LastName;
        EditableGunnery = Gunnery;
        EditablePiloting = Piloting;
    }
}
