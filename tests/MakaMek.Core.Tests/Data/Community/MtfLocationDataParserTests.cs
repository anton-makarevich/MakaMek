using NSubstitute;
using Shouldly;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using LocationSlotComponents = System.Collections.Generic.Dictionary<
    Sanet.MakaMek.Core.Models.Units.PartLocation,
    System.Collections.Generic.SortedDictionary<
        int,
        (Sanet.MakaMek.Core.Data.Units.Components.MakaMekComponent component,
         Sanet.MakaMek.Core.Data.Units.Components.ComponentOptions options)>>;

namespace Sanet.MakaMek.Core.Tests.Data.Community;

public class MtfLocationDataParserTests
{
    private readonly IComponentProvider _componentProvider = Substitute.For<IComponentProvider>();
    private readonly Dictionary<MakaMekComponent, int> _componentSizes = new();
    private readonly HashSet<MakaMekComponent> _undefinedComponents = [];

    public MtfLocationDataParserTests()
    {
        _componentProvider
            .GetDefinition(Arg.Any<MakaMekComponent>(), Arg.Any<ComponentSpecificData?>())
            .Returns(call =>
            {
                var component = call.Arg<MakaMekComponent>();
                return _undefinedComponents.Contains(component)
                    ? null
                    : new EquipmentDefinition(
                        "Test component",
                        component,
                        Size: _componentSizes.GetValueOrDefault(component, 1));
            });
    }

    [Fact]
    public void Parse_ParsesFrontAndRearArmor()
    {
        // Act
        var (_, armor) = Parse([
            "Armor:Standard(Inner Sphere)",
            "RTL Armor:5", // creates the main location before its front armor line
            "LT Armor:9",
            "RT Armor:9",
            "RTR Armor:4", // reuses the main location created by the front armor line
            "LA Armor:5",
            "LA Armor:7" // duplicate line overwrites the front value
        ]);

        // Assert
        armor[PartLocation.LeftTorso].RearArmor.ShouldBe(5);
        armor[PartLocation.LeftTorso].FrontArmor.ShouldBe(9);
        armor[PartLocation.RightTorso].FrontArmor.ShouldBe(9);
        armor[PartLocation.RightTorso].RearArmor.ShouldBe(4);
        armor[PartLocation.LeftArm].FrontArmor.ShouldBe(7);
    }

    [Fact]
    public void Parse_IgnoresArmorLinesThatDoNotMatchThePattern()
    {
        // Act
        var (equipment, armor) = Parse([
            "Armor:Standard",
            "not an armor line"
        ]);

        // Assert
        equipment.ShouldBeEmpty();
        armor.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_WeaponsMarkerEndsArmorSection()
    {
        // Act
        var (equipment, armor) = Parse([
            "Armor:Standard",
            "LA Armor:5",
            "Weapons:1",
            "Left Arm:",
            "Medium Laser"
        ]);

        // Assert
        armor.Count.ShouldBe(1);
        armor[PartLocation.LeftArm].FrontArmor.ShouldBe(5);
        equipment.ShouldHaveSingleItem().Type.ShouldBe(MakaMekComponent.MediumLaser);
    }

    [Fact]
    public void Parse_RepeatedLocationHeader_RestartsSlotIndex()
    {
        // Act
        var (equipment, _) = Parse([
            "Left Arm:",
            "Medium Laser",
            "Left Arm:",
            "Machine Gun"
        ]);

        // Assert
        var component = equipment.ShouldHaveSingleItem();
        component.Type.ShouldBe(MakaMekComponent.MachineGun);
        component.Assignments.ShouldHaveSingleItem().FirstSlot.ShouldBe(0);
    }

    [Fact]
    public void Parse_StopsAtBlankLineWhenCurrentLocationIsRightLeg()
    {
        // Act
        var (equipment, _) = Parse([
            "Right Leg:",
            "Hip",
            "",
            "Left Arm:",
            "Medium Laser"
        ]);

        // Assert
        equipment.ShouldHaveSingleItem().Type.ShouldBe(MakaMekComponent.Hip);
    }

    [Fact]
    public void Parse_ContinuesAfterBlankLineInOtherLocations()
    {
        // Act
        var (equipment, _) = Parse([
            "Left Arm:",
            "Medium Laser",
            "   ",
            "Right Leg:",
            "Hip"
        ]);

        // Assert
        equipment.Select(component => component.Type)
            .ShouldBe([MakaMekComponent.MediumLaser, MakaMekComponent.Hip]);
    }

    [Fact]
    public void Parse_ThrowsForUnknownLocationHeader()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => Parse(["Unknown Location:", "Medium Laser"]))
            .Message.ShouldContain("Unknown location: Unknown Location");
    }

    [Fact]
    public void Parse_IgnoresEquipmentLinesBeforeAnyLocationHeader()
    {
        // Act
        var (equipment, _) = Parse(["Medium Laser"]);

        // Assert
        equipment.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_EmptySlotsAdvanceTheSlotIndex()
    {
        // Act
        var (equipment, _) = Parse([
            "Left Arm:",
            "Medium Laser",
            "-empty-",
            "Machine Gun"
        ]);

        // Assert
        equipment.Count.ShouldBe(2);
        equipment[0].Type.ShouldBe(MakaMekComponent.MediumLaser);
        equipment[0].Assignments.ShouldHaveSingleItem().FirstSlot.ShouldBe(0);
        equipment[1].Type.ShouldBe(MakaMekComponent.MachineGun);
        equipment[1].Assignments.ShouldHaveSingleItem().FirstSlot.ShouldBe(2);
    }

    [Fact]
    public void Parse_RearFacingSuffix_SetsRearMountingAndStripsSuffix()
    {
        // Act
        var (equipment, _) = Parse(["Left Arm:", "Medium Laser (r)"]);

        // Assert
        var component = equipment.ShouldHaveSingleItem();
        component.Type.ShouldBe(MakaMekComponent.MediumLaser);
        component.SpecificData.ShouldBe(new WeaponStateData(MountingOptions.Rear));
    }

    [Fact]
    public void Parse_FullAmmoSuffix_IsStrippedWithoutSpecificData()
    {
        // Act
        var (equipment, _) = Parse(["Left Torso:", "IS Ammo AC/5 - full"]);

        // Assert
        var ammo = equipment.ShouldHaveSingleItem();
        ammo.Type.ShouldBe(MakaMekComponent.ISAmmoAC5);
        ammo.SpecificData.ShouldBeNull();
    }

    [Fact]
    public void Parse_HalfAmmoSuffix_SetsMassRoundsMultiplier()
    {
        // Act
        var (equipment, _) = Parse(["Left Torso:", "IS Ammo AC/5 - Half"]);

        // Assert
        var ammo = equipment.ShouldHaveSingleItem();
        ammo.Type.ShouldBe(MakaMekComponent.ISAmmoAC5);
        ammo.SpecificData.ShouldBe(new AmmoStateData(null, 0.5m));
    }

    [Fact]
    public void Parse_EngineWithRatingAndType_SetsEngineStateData()
    {
        // Act
        var (equipment, _) = Parse(
            ["Center Torso:", "Fusion Engine"],
            new Dictionary<string, string>
            {
                ["engine-rating"] = "275",
                ["engine-type"] = "Fusion"
            });

        // Assert
        var engine = equipment.ShouldHaveSingleItem();
        engine.Type.ShouldBe(MakaMekComponent.Engine);
        engine.SpecificData.ShouldBe(new EngineStateData(EngineType.Fusion, 275));
    }

    [Theory]
    [InlineData(null, "Fusion")] // missing rating
    [InlineData("not-a-number", "Fusion")] // unparsable rating
    [InlineData("275", null)] // missing type
    [InlineData("275", "NotAnEngineType")] // unparsable type
    public void Parse_EngineWithIncompleteData_LeavesSpecificDataNull(string? rating, string? engineType)
    {
        // Arrange
        var mechData = new Dictionary<string, string>();
        if (rating is not null) mechData["engine-rating"] = rating;
        if (engineType is not null) mechData["engine-type"] = engineType;

        // Act
        var (equipment, _) = Parse(["Center Torso:", "Fusion Engine"], mechData);

        // Assert
        equipment.ShouldHaveSingleItem().SpecificData.ShouldBeNull();
    }

    [Fact]
    public void Parse_WhenComponentDefinitionIsMissing_Throws()
    {
        // Arrange
        _undefinedComponents.Add(MakaMekComponent.MachineGun);

        // Act & Assert
        Should.Throw<ArgumentException>(() => Parse(["Left Arm:", "Machine Gun"]))
            .Message.ShouldContain("No definition found for component: MachineGun");
    }

    [Fact]
    public void Parse_ConsecutiveSlotsAreMergedIntoOneAssignment()
    {
        // Arrange
        _componentSizes[MakaMekComponent.AC5] = 4;

        // Act
        var (equipment, _) = Parse([
            "Left Torso:",
            "Autocannon/5",
            "Autocannon/5",
            "Autocannon/5",
            "Autocannon/5"
        ]);

        // Assert
        var autocannon = equipment.ShouldHaveSingleItem();
        autocannon.Type.ShouldBe(MakaMekComponent.AC5);
        autocannon.Assignments.ShouldHaveSingleItem()
            .ShouldBe(new LocationSlotAssignment(PartLocation.LeftTorso, 0, 4));
    }

    [Fact]
    public void Parse_MultiSlotComponentIsSplitAcrossLocations()
    {
        // Arrange
        _componentSizes[MakaMekComponent.AC5] = 4;

        // Act
        var (equipment, _) = Parse([
            "Left Torso:",
            "Autocannon/5",
            "Autocannon/5",
            "Right Torso:",
            "Autocannon/5",
            "Autocannon/5",
            "Left Leg:",
            "Machine Gun"
        ]);

        // Assert
        equipment.Count.ShouldBe(2);
        equipment[0].Type.ShouldBe(MakaMekComponent.AC5);
        equipment[0].Assignments.ShouldBe([
            new LocationSlotAssignment(PartLocation.LeftTorso, 0, 2),
            new LocationSlotAssignment(PartLocation.RightTorso, 0, 2)
        ]);
        equipment[1].Type.ShouldBe(MakaMekComponent.MachineGun);
        equipment[1].Assignments.ShouldHaveSingleItem().Location.ShouldBe(PartLocation.LeftLeg);
    }

    [Fact]
    public void Parse_MultiSlotComponentWithTooFewSlots_ReturnsPartialAssignment()
    {
        // Arrange
        _componentSizes[MakaMekComponent.AC5] = 3;

        // Act
        var (equipment, _) = Parse(["Left Torso:", "Autocannon/5"]);

        // Assert
        equipment.ShouldHaveSingleItem().Assignments
            .ShouldHaveSingleItem()
            .ShouldBe(new LocationSlotAssignment(PartLocation.LeftTorso, 0, 1));
    }

    [Fact]
    public void Parse_MultiSlotComponentStopsAtDifferentComponent()
    {
        // Arrange
        _componentSizes[MakaMekComponent.AC5] = 4;

        // Act
        var (equipment, _) = Parse([
            "Left Torso:",
            "Autocannon/5",
            "Machine Gun",
            "Autocannon/5",
            "Autocannon/5"
        ]);

        // Assert
        equipment.Count.ShouldBe(2);
        equipment[0].Type.ShouldBe(MakaMekComponent.AC5);
        equipment[0].Assignments.ShouldBe([
            new LocationSlotAssignment(PartLocation.LeftTorso, 0, 1),
            new LocationSlotAssignment(PartLocation.LeftTorso, 2, 2)
        ]);
        equipment[1].Type.ShouldBe(MakaMekComponent.MachineGun);
    }

    [Fact]
    public void FindConsecutiveSlots_WhenLocationIsMissing_ReturnsEmpty()
    {
        // Arrange
        var locationSlotComponents = new LocationSlotComponents
        {
            [PartLocation.LeftTorso] = SlotMap((0, MakaMekComponent.MachineGun))
        };

        // Act
        var result = MtfLocationDataParser.FindConsecutiveSlots(
            MakaMekComponent.MachineGun,
            PartLocation.LeftArm,
            0,
            locationSlotComponents,
            new HashSet<(PartLocation, int)>(),
            1);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void FindConsecutiveSlots_WhenSlotIsMissing_ReturnsEmpty()
    {
        // Arrange
        var locationSlotComponents = new LocationSlotComponents
        {
            [PartLocation.LeftArm] = SlotMap((0, MakaMekComponent.MachineGun))
        };

        // Act
        var result = MtfLocationDataParser.FindConsecutiveSlots(
            MakaMekComponent.MachineGun,
            PartLocation.LeftArm,
            5,
            locationSlotComponents,
            new HashSet<(PartLocation, int)>(),
            1);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void FindConsecutiveSlots_WhenComponentDiffers_ReturnsEmpty()
    {
        // Arrange
        var locationSlotComponents = new LocationSlotComponents
        {
            [PartLocation.LeftArm] = SlotMap((0, MakaMekComponent.MachineGun))
        };

        // Act
        var result = MtfLocationDataParser.FindConsecutiveSlots(
            MakaMekComponent.MediumLaser,
            PartLocation.LeftArm,
            0,
            locationSlotComponents,
            new HashSet<(PartLocation, int)>(),
            1);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void FindConsecutiveSlots_WhenSlotAlreadyProcessed_ReturnsEmpty()
    {
        // Arrange
        var locationSlotComponents = new LocationSlotComponents
        {
            [PartLocation.LeftArm] = SlotMap((0, MakaMekComponent.MachineGun))
        };
        var processedSlots = new HashSet<(PartLocation, int)> { (PartLocation.LeftArm, 0) };

        // Act
        var result = MtfLocationDataParser.FindConsecutiveSlots(
            MakaMekComponent.MachineGun,
            PartLocation.LeftArm,
            0,
            locationSlotComponents,
            processedSlots,
            1);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void FindConsecutiveSlots_RespectsMaxSlots()
    {
        // Arrange
        var locationSlotComponents = new LocationSlotComponents
        {
            [PartLocation.LeftArm] = SlotMap(
                (0, MakaMekComponent.MediumLaser),
                (1, MakaMekComponent.MediumLaser),
                (2, MakaMekComponent.MediumLaser))
        };
        var processedSlots = new HashSet<(PartLocation, int)>();

        // Act
        var result = MtfLocationDataParser.FindConsecutiveSlots(
            MakaMekComponent.MediumLaser,
            PartLocation.LeftArm,
            0,
            locationSlotComponents,
            processedSlots,
            2);

        // Assert
        result.ShouldHaveSingleItem().ShouldBe(new LocationSlotAssignment(PartLocation.LeftArm, 0, 2));
        processedSlots.ShouldContain((PartLocation.LeftArm, 0));
        processedSlots.ShouldContain((PartLocation.LeftArm, 1));
        processedSlots.ShouldNotContain((PartLocation.LeftArm, 2));
    }

    private (List<ComponentData> equipment, Dictionary<PartLocation, ArmorLocation> armor) Parse(
        IReadOnlyList<string> lines,
        Dictionary<string, string>? mechData = null) =>
        MtfLocationDataParser.Parse(
            lines,
            mechData ?? new Dictionary<string, string>(),
            _componentProvider);

    private static SortedDictionary<int, (MakaMekComponent component, ComponentOptions options)> SlotMap(
        params (int slot, MakaMekComponent component)[] slots)
    {
        var map = new SortedDictionary<int, (MakaMekComponent component, ComponentOptions options)>();
        foreach (var (slot, component) in slots)
            map[slot] = (component, new ComponentOptions());
        return map;
    }
}

