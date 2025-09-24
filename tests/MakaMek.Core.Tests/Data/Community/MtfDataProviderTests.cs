using System.Reflection;
using Shouldly;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;

namespace Sanet.MakaMek.Core.Tests.Data.Community;

public class MtfDataProviderTests
{
    private readonly string[] _locustMtfData = File.ReadAllLines("Resources/Mechs/LCT-1V.mtf");
    private readonly string[] _shadowHawkMtfData = File.ReadAllLines("Resources/Mechs/SHD-2D.mtf");
    private readonly IComponentProvider _componentProvider = new ClassicBattletechComponentProvider();

    [Fact]
    public void Parse_LocustMtf_ReturnsCorrectBasicData()
    {
        // Arrange
        var sut = new MtfDataProvider(_componentProvider);

        // Act
        var mechData = sut.LoadMechFromTextData(_locustMtfData);

        // Assert
        mechData.Chassis.ShouldBe("Locust");
        mechData.Model.ShouldBe("LCT-1V");
        mechData.Mass.ShouldBe(20);
        mechData.WalkMp.ShouldBe(8);
        mechData.EngineRating.ShouldBe(160);
        mechData.EngineType.ShouldBe("Fusion");
    }

    [Fact]
    public void Parse_LocustMtf_ReturnsCorrectArmorValues()
    {
        // Arrange
        var sut = new MtfDataProvider(_componentProvider);

        // Act
        var mechData = sut.LoadMechFromTextData(_locustMtfData);

        // Assert
        mechData.ArmorValues[PartLocation.LeftArm].FrontArmor.ShouldBe(4);
        mechData.ArmorValues[PartLocation.RightArm].FrontArmor.ShouldBe(4);
        mechData.ArmorValues[PartLocation.LeftTorso].FrontArmor.ShouldBe(8);
        mechData.ArmorValues[PartLocation.RightTorso].FrontArmor.ShouldBe(8);
        mechData.ArmorValues[PartLocation.CenterTorso].FrontArmor.ShouldBe(10);
        mechData.ArmorValues[PartLocation.Head].FrontArmor.ShouldBe(8);
        mechData.ArmorValues[PartLocation.LeftLeg].FrontArmor.ShouldBe(8);
        mechData.ArmorValues[PartLocation.RightLeg].FrontArmor.ShouldBe(8);
    }

    [Fact]
    public void Parse_LocustMtf_ReturnsCorrectEquipmentWithSlotPositions()
    {
        // Arrange
        var sut = new MtfDataProvider(_componentProvider);

        // Act
        var mechData = sut.LoadMechFromTextData(_locustMtfData);

        // Assert
        // Left Arm - verify exact slot positions
        VerifyArmEquipmentSlotPositions(mechData, PartLocation.LeftArm);

        // Right Arm - verify exact slot positions
        VerifyArmEquipmentSlotPositions(mechData, PartLocation.RightArm);

        // Center Torso - verify exact slot positions based on the MTF file
        const PartLocation location = PartLocation.CenterTorso;
        
        var engine = mechData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.Engine
            && cd.Assignments.Any(a => a.Location == location));
        engine.ShouldNotBeNull();
        engine.Assignments.Count.ShouldBe(2);
        engine.Assignments[0].FirstSlot.ShouldBe(0);
        engine.Assignments[0].Length.ShouldBe(3);
        engine.Assignments[1].FirstSlot.ShouldBe(7);
        engine.Assignments[1].Length.ShouldBe(3);
        
        var gyro = mechData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.Gyro
            && cd.Assignments.Any(a => a.Location == location));
        gyro.ShouldNotBeNull();
        gyro.Assignments.Count.ShouldBe(1);
        gyro.Assignments[0].FirstSlot.ShouldBe(3);
        gyro.Assignments[0].Length.ShouldBe(4); 
        
        var mediumLaser = mechData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.MediumLaser
            && cd.Assignments.Any(a => a.Location == location));
        mediumLaser.ShouldNotBeNull();
        mediumLaser.Assignments.Count.ShouldBe(1);
        mediumLaser.Assignments[0].FirstSlot.ShouldBe(10);
        mediumLaser.Assignments[0].Length.ShouldBe(1);
        
        var ammo = mechData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.ISAmmoMG
            && cd.Assignments.Any(a => a.Location == location));
        ammo.ShouldNotBeNull();
        ammo.Assignments.Count.ShouldBe(1);
        ammo.Assignments[0].FirstSlot.ShouldBe(11);
        ammo.Assignments[0].Length.ShouldBe(1);

        void VerifyArmEquipmentSlotPositions(UnitData unitData, PartLocation partLocation)
        {
            var shoulder = unitData.Equipment
                .FirstOrDefault(cd => cd.Type == MakaMekComponent.Shoulder 
                                      && cd.Assignments.Any(a => a.Location == partLocation));
            shoulder.ShouldNotBeNull();
            shoulder.Assignments[0].FirstSlot.ShouldBe(0);
        
            var upperArm = unitData.Equipment
                .FirstOrDefault(cd => cd.Type == MakaMekComponent.UpperArmActuator 
                                      && cd.Assignments.Any(a => a.Location == partLocation));
            upperArm.ShouldNotBeNull();
            upperArm.Assignments[0].FirstSlot.ShouldBe(1);

            var machineGun = unitData.Equipment
                .FirstOrDefault(cd => cd.Type == MakaMekComponent.MachineGun 
                                      && cd.Assignments.Any(a => a.Location == partLocation));
            machineGun.ShouldNotBeNull();
            machineGun.Assignments[0].FirstSlot.ShouldBe(2);
        }
    }

    [Fact]
    public void Parse_LocustMtf_HandlesEmptySlots()
    {
        // Arrange
        var sut = new MtfDataProvider(_componentProvider);

        // Act
        var mechData = sut.LoadMechFromTextData(_locustMtfData);

        // Assert
        var leftTorsoLayout = mechData.Equipment.Select(cd => cd.Assignments)
            .Where(a => a.Any(assignment => assignment.Location == PartLocation.LeftTorso))
            .ToList();

        // Left Torso should be completely empty 
        leftTorsoLayout.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_LocustMtf_VerifiesHeadSlotLayout()
    {
        // Arrange
        var sut = new MtfDataProvider(_componentProvider);

        // Act
        var mechData = sut.LoadMechFromTextData(_locustMtfData);

        // Assert
        const PartLocation location = PartLocation.Head;

        // Based on the MTF file structure
        var lifeSupport = mechData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.LifeSupport
            && cd.Assignments.Any(a => a.Location == location));
        lifeSupport.ShouldNotBeNull();
        lifeSupport.Assignments.Count.ShouldBe(2);
        lifeSupport.Assignments[0].FirstSlot.ShouldBe(0);
        lifeSupport.Assignments[0].Length.ShouldBe(1);
        lifeSupport.Assignments[1].FirstSlot.ShouldBe(5);
        lifeSupport.Assignments[1].Length.ShouldBe(1);
        
        var sensors = mechData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.Sensors
            && cd.Assignments.Any(a => a.Location == location));
        sensors.ShouldNotBeNull();
        sensors.Assignments.Count.ShouldBe(2);
        sensors.Assignments[0].FirstSlot.ShouldBe(1);
        sensors.Assignments[0].Length.ShouldBe(1);
        sensors.Assignments[1].FirstSlot.ShouldBe(4);
        sensors.Assignments[1].Length.ShouldBe(1);
        
        var cockpit = mechData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.Cockpit
            && cd.Assignments.Any(a => a.Location == location));
        cockpit.ShouldNotBeNull();
        cockpit.Assignments.Count.ShouldBe(1);
        cockpit.Assignments[0].FirstSlot.ShouldBe(2);
        cockpit.Assignments[0].Length.ShouldBe(1);
    }

    [Fact]
    public void Parse_LocustMtf_HandlesSequentialHeatSinks()
    {
        // Arrange
        var sut = new MtfDataProvider(_componentProvider);

        // Act
        var mechData = sut.LoadMechFromTextData(_locustMtfData);

        // Assert - Heat sinks in legs should be treated as separate components
        var leftLegHeatSinks = mechData.Equipment
            .Where(cd => cd.Type == MakaMekComponent.HeatSink &&
                        cd.Assignments.Any(a => a.Location == PartLocation.LeftLeg))
            .ToList();

        var rightLegHeatSinks = mechData.Equipment
            .Where(cd => cd.Type == MakaMekComponent.HeatSink &&
                        cd.Assignments.Any(a => a.Location == PartLocation.RightLeg))
            .ToList();

        // Should have 2 separate heat sink components in each leg
        leftLegHeatSinks.Count.ShouldBe(2);
        rightLegHeatSinks.Count.ShouldBe(2);

        // Each heat sink should occupy exactly 1 slot
        foreach (var heatSink in leftLegHeatSinks.Concat(rightLegHeatSinks))
        {
            heatSink.Assignments.Count.ShouldBe(1);
            heatSink.Assignments[0].Length.ShouldBe(1);
        }
    }

    [Fact]
    public void Parse_LocustMtf_HandlesEngineWithCorrectSize()
    {
        // Arrange
        var sut = new MtfDataProvider(_componentProvider);

        // Act
        var mechData = sut.LoadMechFromTextData(_locustMtfData);

        // Assert - Engine should be properly sized and have engine state data
        var engine = mechData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.Engine);
        engine.ShouldNotBeNull();

        // Engine should have EngineStateData
        engine.SpecificData.ShouldBeOfType<EngineStateData>();
        var engineData = (EngineStateData)engine.SpecificData!;
        engineData.Type.ShouldBe(EngineType.Fusion);
        engineData.Rating.ShouldBe(160);

        // Standard fusion engine should occupy 6 slots total
        engine.Assignments.Count.ShouldBe(2);
        engine.Assignments[0].Length.ShouldBe(3);
        engine.Assignments[1].Length.ShouldBe(3);
        engine.Assignments[0].FirstSlot.ShouldBe(0);
        engine.Assignments[1].FirstSlot.ShouldBe(7);
        var totalSlots = engine.Assignments.Sum(a => a.Length);
        totalSlots.ShouldBe(6);

        // All slots should be in Center Torso for the standard fusion engine
        engine.Assignments.All(a => a.Location == PartLocation.CenterTorso).ShouldBeTrue();
    }

    [Fact]
    public void Parse_ShadowHawkMtf_HandlesMultiSlotWeapons()
    {
        // Arrange
        var sut = new MtfDataProvider(_componentProvider);

        // Act
        var mechData = sut.LoadMechFromTextData(_shadowHawkMtfData);

        // Assert - AC/5 should occupy 4 slots in Left Torso
        var ac5 = mechData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.AC5);
        ac5.ShouldNotBeNull();

        // AC/5 should have exactly one assignment in Left Torso
        ac5.Assignments.Count.ShouldBe(1);
        ac5.Assignments[0].Location.ShouldBe(PartLocation.LeftTorso);
        ac5.Assignments[0].Length.ShouldBe(4); // AC/5 is 4 slots
        ac5.Assignments[0].FirstSlot.ShouldBe(1); // Should start at slot 1 (after Jump Jet at slot 0)
    }

    [Fact]
    public void Parse_ShadowHawkMtf_HandlesSequentialHeatSinksInRightTorso()
    {
        // Arrange
        var sut = new MtfDataProvider(_componentProvider);

        // Act
        var mechData = sut.LoadMechFromTextData(_shadowHawkMtfData);

        // Assert - Right Torso should have 3 separate heat sink components
        var rightTorsoHeatSinks = mechData.Equipment
            .Where(cd => cd.Type == MakaMekComponent.HeatSink &&
                        cd.Assignments.Any(a => a.Location == PartLocation.RightTorso))
            .ToList();

        // Should have 3 separate heat sink components
        rightTorsoHeatSinks.Count.ShouldBe(3);

        // Each heat sink should occupy exactly 1 slot
        foreach (var heatSink in rightTorsoHeatSinks)
        {
            heatSink.Assignments.Count.ShouldBe(1);
            heatSink.Assignments[0].Length.ShouldBe(1);
            heatSink.Assignments[0].Location.ShouldBe(PartLocation.RightTorso);
        }

        // Heat sinks should be in slots 0, 1, 2
        var slots = rightTorsoHeatSinks.Select(hs => hs.Assignments[0].FirstSlot).OrderBy(s => s).ToArray();
        slots.ShouldBe([0, 1, 2]);
    }

    [Fact]
    public void Parse_CustomMtf_WithInsufficientSlotsForMultiSlotComponent_ReturnsPartialAssignment()
    {
        // Arrange
        var customMtfData = _shadowHawkMtfData;
        customMtfData[84] = "-Empty-";

        var sut = new MtfDataProvider(_componentProvider);

        // Act
        var mechData = sut.LoadMechFromTextData(_shadowHawkMtfData);

        // Assert
        var ac5 = mechData.Equipment.FirstOrDefault(cd => cd.Type == MakaMekComponent.AC5);
        ac5.ShouldNotBeNull();
        ac5.Assignments.Count.ShouldBe(1);
        ac5.Assignments[0].Location.ShouldBe(PartLocation.LeftTorso);
        ac5.Assignments[0].Length.ShouldBe(3);
    }

    [Fact]
    public void FindConsecutiveSlotsInLocation_WhenSlotAlreadyProcessed_ReturnsEmptyAssignments()
    {
        // Arrange
        var sut = new MtfDataProvider(_componentProvider);
        var locationSlotComponents = new Dictionary<PartLocation, Dictionary<int, MakaMekComponent>>
        {
            [PartLocation.LeftArm] = new()
            {
                [0] = MakaMekComponent.MachineGun
            }
        };
        var processedSlots = new HashSet<(PartLocation, int)>
        {
            (PartLocation.LeftArm, 0)
        };

        var method = typeof(MtfDataProvider).GetMethod(
            "FindConsecutiveSlotsInLocation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.ShouldNotBeNull();

        // Act
        var result = method.Invoke(sut, [
            MakaMekComponent.MachineGun,
            PartLocation.LeftArm,
            0,
            locationSlotComponents,
            processedSlots,
            1
        ]) as List<LocationSlotAssignment>;

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
}
