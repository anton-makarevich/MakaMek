using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class PilotViewModelTests
{
    [Fact]
    public void StartEditing_CopiesCurrentValuesToEditable()
    {
        var pilot = new MechWarrior("John", "Doe", gunnery: 3, piloting: 5);
        var sut = new PilotViewModel(pilot);

        sut.StartEditing();

        sut.EditableFirstName.ShouldBe("John");
        sut.EditableLastName.ShouldBe("Doe");
        sut.EditableGunnery.ShouldBe(3);
        sut.EditablePiloting.ShouldBe(5);
    }

    [Fact]
    public void SaveEdit_UpdatesFirstNameAndLastName()
    {
        var pilot = new MechWarrior("John", "Doe", gunnery: 3, piloting: 5);
        var sut = new PilotViewModel(pilot);
        sut.StartEditing();

        sut.EditableFirstName = "Jane";
        sut.EditableLastName = "Smith";
        var result = sut.SaveEdit();

        result.FirstName.ShouldBe("Jane");
        result.LastName.ShouldBe("Smith");
    }

    [Fact]
    public void SaveEdit_TrimsWhitespaceFromNames()
    {
        var pilot = new MechWarrior("John", "Doe", gunnery: 3, piloting: 5);
        var sut = new PilotViewModel(pilot);
        sut.StartEditing();

        sut.EditableFirstName = "  Jane  ";
        sut.EditableLastName = "  Smith  ";
        var result = sut.SaveEdit();

        result.FirstName.ShouldBe("Jane");
        result.LastName.ShouldBe("Smith");
    }

    [Fact]
    public void SaveEdit_DefaultsEmptyFirstNameToMechWarrior()
    {
        var pilot = new MechWarrior("John", "Doe", gunnery: 3, piloting: 5);
        var sut = new PilotViewModel(pilot);
        sut.StartEditing();

        sut.EditableFirstName = "";
        var result = sut.SaveEdit();

        result.FirstName.ShouldBe("MechWarrior");
    }

    [Fact]
    public void SaveEdit_ClampsGunneryToRange()
    {
        var pilot = new MechWarrior("John", "Doe", gunnery: 4, piloting: 5);
        var sut = new PilotViewModel(pilot);
        sut.StartEditing();

        sut.EditableGunnery = 10;
        var resultHigh = sut.SaveEdit();
        resultHigh.Gunnery.ShouldBe(8);

        sut.StartEditing();
        sut.EditableGunnery = -5;
        var resultLow = sut.SaveEdit();
        resultLow.Gunnery.ShouldBe(0);
    }

    [Fact]
    public void SaveEdit_ClampsPilotingToRange()
    {
        var pilot = new MechWarrior("John", "Doe", gunnery: 4, piloting: 5);
        var sut = new PilotViewModel(pilot);
        sut.StartEditing();

        sut.EditablePiloting = 10;
        var resultHigh = sut.SaveEdit();
        resultHigh.Piloting.ShouldBe(8);

        sut.StartEditing();
        sut.EditablePiloting = -5;
        var resultLow = sut.SaveEdit();
        resultLow.Piloting.ShouldBe(0);
    }

    [Fact]
    public void CancelEdit_ResetsEditableToOriginal()
    {
        var pilot = new MechWarrior("John", "Doe", gunnery: 3, piloting: 5);
        var sut = new PilotViewModel(pilot);
        sut.StartEditing();

        sut.EditableFirstName = "Jane";
        sut.EditableLastName = "Smith";
        sut.EditableGunnery = 1;
        sut.EditablePiloting = 2;

        sut.CancelEdit();

        sut.EditableFirstName.ShouldBe("John");
        sut.EditableLastName.ShouldBe("Doe");
        sut.EditableGunnery.ShouldBe(3);
        sut.EditablePiloting.ShouldBe(5);
    }
}
