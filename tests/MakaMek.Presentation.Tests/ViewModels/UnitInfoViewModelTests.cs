using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Tests.Utils;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels;

public class UnitInfoViewModelTests
{
    private readonly IMechFactory _mechFactory = Substitute.For<IMechFactory>();

    [Fact]
    public void Constructor_SetsUnitFromFactory()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory);

        sut.Unit.ShouldBe(unit);
        sut.HasPilot.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_SetsHasPilotTrue_WhenPilotDataProvided()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var pilotData = new PilotData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, pilotData, _mechFactory);

        sut.HasPilot.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_AssignsPilotToUnit_WhenPilotDataProvided()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var pilotData = new PilotData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, pilotData, _mechFactory);

        unit.Pilot.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetResultAsync_ReturnsTaskThatCompletes_WhenCloseCommandExecuted()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory);

        var resultTask = sut.GetResultAsync();
        resultTask.IsCompleted.ShouldBeFalse();

        sut.CloseCommand.Execute(null);

        var result = await resultTask;
        result.ShouldBeNull();
    }

    [Fact]
    public void CanEdit_IsFalseByDefault()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory);

        sut.CanEdit.ShouldBeFalse();
    }

    [Fact]
    public void CanEdit_IsTrue_WhenPassedTrue()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory, canEdit: true);

        sut.CanEdit.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveCommand_ResolvesResultWithEditedPilotData()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);
        var pilotData = new PilotData
        {
            FirstName = "John",
            LastName = "Doe",
            Gunnery = 4,
            Piloting = 5
        };

        var sut = new UnitInfoViewModel(unitData, pilotData, _mechFactory, canEdit: true);

        var resultTask = sut.GetResultAsync();
        sut.Pilot!.EditableFirstName = "Jane";
        sut.Pilot!.EditableLastName = "Smith";
        sut.SaveCommand.Execute(null);

        var result = await resultTask;
        result.ShouldNotBeNull();
        result.PilotData.FirstName.ShouldBe("Jane");
        result.PilotData.LastName.ShouldBe("Smith");
    }

    [Fact]
    public async Task SaveCommand_ResolvesResultWithEditedUnitName()
    {
        var unitData = MechFactoryTests.CreateDummyMechData() with { Name = "Original" };
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory, canEdit: true);

        var resultTask = sut.GetResultAsync();
        sut.StartEditingName();
        sut.EditableName = "Renamed";
        sut.SaveName();
        sut.SaveCommand.Execute(null);

        var result = await resultTask;
        result.ShouldNotBeNull();
        result.UnitData.Name.ShouldBe("Renamed");
    }

    [Fact]
    public async Task CloseCommand_ResolvesWithNull()
    {
        var unitData = MechFactoryTests.CreateDummyMechData();
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory, canEdit: true);

        var resultTask = sut.GetResultAsync();
        sut.CloseCommand.Execute(null);

        var result = await resultTask;
        result.ShouldBeNull();
    }

    [Fact]
    public void StartEditingName_SetsEditableNameFromOriginal()
    {
        var unitData = MechFactoryTests.CreateDummyMechData() with { Name = "My Mech" };
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory, canEdit: true);

        sut.StartEditingName();

        sut.IsEditingName.ShouldBeTrue();
        sut.EditableName.ShouldBe("My Mech");
    }

    [Fact]
    public void SaveName_UpdatesDisplayName()
    {
        var unitData = MechFactoryTests.CreateDummyMechData() with { Name = "Original" };
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory, canEdit: true);

        sut.StartEditingName();
        sut.EditableName = "Updated";
        sut.SaveName();

        sut.DisplayName.ShouldBe("Updated");
        sut.IsEditingName.ShouldBeFalse();
    }

    [Fact]
    public void CancelEditName_ResetsIsEditingName()
    {
        var unitData = MechFactoryTests.CreateDummyMechData() with { Name = "Original" };
        var unit = CreateRealUnit(unitData);
        _mechFactory.Create(unitData).Returns(unit);

        var sut = new UnitInfoViewModel(unitData, null, _mechFactory, canEdit: true);

        sut.StartEditingName();
        sut.EditableName = "Something else";
        sut.CancelEditName();

        sut.IsEditingName.ShouldBeFalse();
        sut.EditableName.ShouldBe("Original");
    }

    private static Unit CreateRealUnit(UnitData unitData)
    {
        var factory = new MechFactory(
            new TotalWarfareRulesProvider(),
            new ClassicBattletechComponentProvider(),
            Substitute.For<ILocalizationService>());
        return factory.Create(unitData);
    }
}
