using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Shouldly;

namespace Sanet.MakaMek.Presentation.Tests.ViewModels.Wrappers;

public class UnitViewModelTests
{
    private static UnitData CreateUnitData(string? name = null)
    {
        return new UnitData
        {
            Chassis = "Locust",
            Model = "LCT-1V",
            Name = name,
            Mass = 20,
            EngineRating = 100,
            EngineType = "Fusion",
            ArmorValues = new Dictionary<PartLocation, ArmorLocation>
            {
                { PartLocation.Head, new ArmorLocation { FrontArmor = 9 } },
                { PartLocation.CenterTorso, new ArmorLocation { FrontArmor = 10, RearArmor = 5 } },
                { PartLocation.LeftTorso, new ArmorLocation { FrontArmor = 8, RearArmor = 4 } },
                { PartLocation.RightTorso, new ArmorLocation { FrontArmor = 8, RearArmor = 4 } },
                { PartLocation.LeftArm, new ArmorLocation { FrontArmor = 4 } },
                { PartLocation.RightArm, new ArmorLocation { FrontArmor = 4 } },
                { PartLocation.LeftLeg, new ArmorLocation { FrontArmor = 8 } },
                { PartLocation.RightLeg, new ArmorLocation { FrontArmor = 8 } }
            },
            Equipment = new List<ComponentData>
            {
                new()
                {
                    Type = MakaMekComponent.Engine,
                    Assignments =
                    [
                        new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                        new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
                    ],
                    SpecificData = new EngineStateData(EngineType.Fusion, 100)
                }
            },
            AdditionalAttributes = new Dictionary<string, string>(),
            Quirks = new Dictionary<string, string>()
        };
    }

    [Fact]
    public void DisplayName_WithCustomName_ReturnsCustomName()
    {
        var unitData = CreateUnitData("My Mech");
        var sut = new UnitViewModel(unitData);

        sut.DisplayName.ShouldBe("My Mech");
    }

    [Fact]
    public void DisplayName_WithoutCustomName_FallsBackToChassisAndModel()
    {
        var unitData = CreateUnitData();
        var sut = new UnitViewModel(unitData);

        sut.DisplayName.ShouldBe("Locust LCT-1V");
    }

    [Fact]
    public void DisplayName_WithBlankCustomName_FallsBackToChassisAndModel()
    {
        var unitData = CreateUnitData("   ");
        var sut = new UnitViewModel(unitData);

        sut.DisplayName.ShouldBe("Locust LCT-1V");
    }

    [Fact]
    public void StartEditingName_SetsIsEditingNameAndPopulatesEditableName()
    {
        var unitData = CreateUnitData("Original");
        var sut = new UnitViewModel(unitData);

        sut.StartEditingName();

        sut.IsEditingName.ShouldBeTrue();
        sut.EditableName.ShouldBe("Original");
    }

    [Fact]
    public void StartEditingName_WhenNameIsNull_SetsEditableNameToEmpty()
    {
        var unitData = CreateUnitData();
        var sut = new UnitViewModel(unitData);

        sut.StartEditingName();

        sut.EditableName.ShouldBeEmpty();
    }

    [Fact]
    public void SaveName_WithoutChanges_WhenNameIsNull_PreservesNull()
    {
        var unitData = CreateUnitData();
        var sut = new UnitViewModel(unitData);

        sut.StartEditingName();
        sut.SaveName();

        sut.IsEditingName.ShouldBeFalse();
        sut.UnitData.Name.ShouldBeNull();
        sut.DisplayName.ShouldBe("Locust LCT-1V");
    }

    [Fact]
    public void CanEditName_ReturnsFalse_WhenEditing()
    {
        var unitData = CreateUnitData();
        var sut = new UnitViewModel(unitData);

        sut.StartEditingName();

        sut.CanEditName.ShouldBeFalse();
    }

    [Fact]
    public void CanEditName_ReturnsTrue_WhenNotEditing()
    {
        var unitData = CreateUnitData();
        var sut = new UnitViewModel(unitData);

        sut.CanEditName.ShouldBeTrue();
    }

    [Fact]
    public void SaveName_WithValidName_UpdatesUnitData()
    {
        var unitData = CreateUnitData("Original");
        var sut = new UnitViewModel(unitData);

        sut.StartEditingName();
        sut.EditableName = "New Name";
        sut.SaveName();

        sut.IsEditingName.ShouldBeFalse();
        sut.DisplayName.ShouldBe("New Name");
        sut.UnitData.Name.ShouldBe("New Name");
    }

    [Fact]
    public void SaveName_WithBlankName_RevertsToFallback()
    {
        var unitData = CreateUnitData("Original");
        var sut = new UnitViewModel(unitData);

        sut.StartEditingName();
        sut.EditableName = "   ";
        sut.SaveName();

        sut.IsEditingName.ShouldBeFalse();
        sut.DisplayName.ShouldBe("Locust LCT-1V");
        sut.UnitData.Name.ShouldBeNull();
    }

    [Fact]
    public void SaveName_WhenNotEditing_DoesNotChangeName()
    {
        var unitData = CreateUnitData("Original");
        var sut = new UnitViewModel(unitData);

        sut.SaveName();

        sut.IsEditingName.ShouldBeFalse();
        sut.DisplayName.ShouldBe("Original");
        sut.UnitData.Name.ShouldBe("Original");
    }

    [Fact]
    public void CancelEditName_RevertsEditableNameAndExitsEditing()
    {
        var unitData = CreateUnitData("Original");
        var sut = new UnitViewModel(unitData);

        sut.StartEditingName();
        sut.EditableName = "Changed";
        sut.CancelEditName();

        sut.IsEditingName.ShouldBeFalse();
        sut.EditableName.ShouldBe("Original");
        sut.DisplayName.ShouldBe("Original");
    }

    [Fact]
    public void UnitData_ReturnsUnderlyingData()
    {
        var unitData = CreateUnitData("Test");
        var sut = new UnitViewModel(unitData);

        sut.UnitData.Name.ShouldBe("Test");
        sut.UnitData.Chassis.ShouldBe("Locust");
        sut.UnitData.Model.ShouldBe("LCT-1V");
    }

    [Fact]
    public void Model_ReturnsModelFromUnitData()
    {
        var unitData = CreateUnitData();
        var sut = new UnitViewModel(unitData);

        sut.Model.ShouldBe("LCT-1V");
    }

    [Fact]
    public void Id_ReturnsIdFromUnitData()
    {
        var id = Guid.NewGuid();
        var unitData = CreateUnitData() with { Id = id };
        var sut = new UnitViewModel(unitData);

        sut.Id.ShouldBe(id);
    }

    [Fact]
    public void Chassis_ReturnsChassisFromUnitData()
    {
        var unitData = CreateUnitData();
        var sut = new UnitViewModel(unitData);

        sut.Chassis.ShouldBe("Locust");
    }

    [Fact]
    public void Id_ReturnsEmpty_WhenUnitDataIdIsNull()
    {
        var unitData = CreateUnitData();
        var sut = new UnitViewModel(unitData);

        sut.Id.ShouldBe(Guid.Empty);
    }

    [Fact]
    public void PilotName_ReturnsPilotName_WhenPilotDataProvided()
    {
        var unitData = CreateUnitData();
        var pilotData = new PilotData
        {
            FirstName = "John",
            LastName = "Doe"
        };
        var sut = new UnitViewModel(unitData, pilotData);

        sut.PilotName.ShouldBe("John Doe");
    }

    [Fact]
    public void PilotName_ReturnsFallback_WhenPilotDataIsNull()
    {
        var unitData = CreateUnitData();
        var sut = new UnitViewModel(unitData);

        sut.PilotName.ShouldBe("No Pilot");
    }

    [Fact]
    public void PilotName_ReturnsLocalizedFallback_WhenPilotDataIsNullAndServiceProvided()
    {
        var unitData = CreateUnitData();
        var localizationService = Substitute.For<ILocalizationService>();
        localizationService.GetString("UnitItem_NoPilot").Returns("Sin Piloto");
        var sut = new UnitViewModel(unitData, null, localizationService);

        sut.PilotName.ShouldBe("Sin Piloto");
    }

    [Fact]
    public void PilotName_TrimsSpaces_WhenLastNameIsEmpty()
    {
        var unitData = CreateUnitData();
        var pilotData = new PilotData
        {
            FirstName = "John",
            LastName = ""
        };
        var sut = new UnitViewModel(unitData, pilotData);

        sut.PilotName.ShouldBe("John");
    }

    [Fact]
    public void UpdatePilot_RaisesPropertyChanged()
    {
        var unitData = CreateUnitData();
        var pilotData = new PilotData
        {
            FirstName = "John",
            LastName = "Doe"
        };
        var sut = new UnitViewModel(unitData, pilotData);
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) changedProperties.Add(e.PropertyName);
        };

        var updatedPilot = new PilotData
        {
            FirstName = "Jane",
            LastName = "Smith"
        };
        sut.UpdatePilot(updatedPilot);

        changedProperties.ShouldContain(nameof(sut.PilotName));
        sut.PilotName.ShouldBe("Jane Smith");
    }

    [Fact]
    public void UpdatePilot_UpdatesPilotName()
    {
        var unitData = CreateUnitData();
        var pilotData = new PilotData
        {
            FirstName = "John",
            LastName = "Doe"
        };
        var sut = new UnitViewModel(unitData, pilotData);

        var updatedPilot = new PilotData
        {
            FirstName = "Jane",
            LastName = "Smith"
        };
        sut.UpdatePilot(updatedPilot);

        sut.PilotName.ShouldBe("Jane Smith");
    }

    [Fact]
    public void UpdateUnitData_RaisesPropertyChanged()
    {
        var unitData = CreateUnitData("OldName");
        var sut = new UnitViewModel(unitData);
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null) changedProperties.Add(e.PropertyName);
        };

        var newUnitData = CreateUnitData("NewName");
        sut.UpdateUnitData(newUnitData);

        changedProperties.ShouldContain(nameof(sut.DisplayName));
        changedProperties.ShouldContain(nameof(sut.UnitData));
        sut.DisplayName.ShouldBe("NewName");
    }
}
