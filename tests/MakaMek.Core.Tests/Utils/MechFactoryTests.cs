using NSubstitute;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Utils;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Utils;

public class MechFactoryTests
{
    private readonly MechFactory _mechFactory;

    public MechFactoryTests()
    {
        var structureValueProvider = new ClassicBattletechRulesProvider();
        var componentProvider = new ClassicBattletechComponentProvider();
        _mechFactory = new MechFactory(structureValueProvider, componentProvider,Substitute.For<ILocalizationService>());
    }

    public static UnitData CreateDummyMechData(List<ComponentData>? equipment = null, bool overwriteEquipment = false)
    {
        var engineRating = 100;
        List<ComponentData> defaultEquipment =
        [
            new()
            {
                Type = MakaMekComponent.Engine,
                Assignments =
                [
                    new LocationSlotAssignment(PartLocation.CenterTorso, 0, 3),
                    new LocationSlotAssignment(PartLocation.CenterTorso, 7, 3)
                ],
                SpecificData = new EngineStateData(EngineType.Fusion, engineRating)
            }
        ];
        if (equipment != null)
        {
            if (overwriteEquipment)
            {
                defaultEquipment = equipment;
            }else
            {
                defaultEquipment.AddRange(equipment);
            }
        }
        
        return new UnitData
        {
            Chassis = "Locust",
            Model = "LCT-1V",
            Mass = 20,
            WalkMp = 8,
            EngineRating = engineRating,
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
            Equipment = defaultEquipment,
            AdditionalAttributes = new Dictionary<string, string>(),
            Quirks = new Dictionary<string, string>()
        };
    }

    [Fact]
    public void Create_WithSingleSlotComponent_MountsCorrectly()
    {
        // Arrange - Single slot component (Medium Laser) in one location
        // Note: Slot 0 is occupied by ShoulderActuator, so use slot 1
        var equipment = new List<ComponentData>
        {
            new()
            {
                Type = MakaMekComponent.MediumLaser,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 1)]
            }
        };
        var unitData = CreateDummyMechData(equipment);

        // Act
        var mech = _mechFactory.Create(unitData);

        // Assert
        var rightArm = mech.Parts[PartLocation.RightArm];
        var mediumLasers = rightArm.GetComponents<MediumLaser>().ToList();
        mediumLasers.Count.ShouldBe(1);

        var laser = mediumLasers[0];
        laser.IsMounted.ShouldBeTrue();
        laser.MountedAtFirstLocationSlots.ShouldBe([1]);
        laser.SlotAssignments.Count.ShouldBe(1);
        laser.SlotAssignments[0].Location.ShouldBe(PartLocation.RightArm);
        laser.SlotAssignments[0].Slots.ShouldBe([1]);
    }

    [Fact]
    public void Create_WithMultiSlotComponent_SingleAssignment_MountsCorrectly()
    {
        // Arrange - Multi-slot component (AC/5) with sequential slots
        // Note: Slot 0 is occupied by ShoulderActuator, so use slots 1-4
        var equipment = new List<ComponentData>
        {
            new ComponentData
            {
                Type = MakaMekComponent.AC5,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 4)]
            }
        };
        var unitData = CreateDummyMechData(equipment);

        // Act
        var mech = _mechFactory.Create(unitData);

        // Assert
        var rightArm = mech.Parts[PartLocation.RightArm];
        var autocannons = rightArm.GetComponents<Ac5>().ToList();
        autocannons.Count.ShouldBe(1);

        var ac5 = autocannons[0];
        ac5.IsMounted.ShouldBeTrue();
        ac5.MountedAtFirstLocationSlots.ShouldBe([1, 2, 3, 4]);
        ac5.SlotAssignments.Count.ShouldBe(1);
        ac5.SlotAssignments[0].Location.ShouldBe(PartLocation.RightArm);
        ac5.SlotAssignments[0].Slots.ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    public void Create_WithMultiSlotComponent_MultipleAssignments_SameLocation_MountsCorrectly()
    {
        // Arrange - Multi-slot component with non-consecutive slots in the same location
        var equipment = new List<ComponentData>
        {
            new ComponentData
            {
                Type = MakaMekComponent.Sensors,
                Assignments =
                [
                    new LocationSlotAssignment(PartLocation.RightArm, 2, 1), 
                    new LocationSlotAssignment(PartLocation.RightArm, 4, 1)
                ]
            }
        };
        var unitData = CreateDummyMechData(equipment);

        // Act
        var mech = _mechFactory.Create(unitData);

        // Assert
        var rightArm = mech.Parts[PartLocation.RightArm];
        var sensors = rightArm.GetComponents<Sensors>().ToList();
        sensors.Count.ShouldBe(1);

        var sensor = sensors[0];
        sensor.IsMounted.ShouldBeTrue();
        sensor.MountedAtFirstLocationSlots.ShouldBe([2, 4]);
        sensor.SlotAssignments.Count.ShouldBe(2);

        // Check the first assignment
        sensor.SlotAssignments[0].Location.ShouldBe(PartLocation.RightArm);
        sensor.SlotAssignments[0].Slots.ShouldBe([2]);

        // Check the second assignment
        sensor.SlotAssignments[1].Location.ShouldBe(PartLocation.RightArm);
        sensor.SlotAssignments[1].Slots.ShouldBe([4]);
    }

    [Fact]
    public void Create_WithMultiLocationComponent_MountsCorrectly()
    {
        // Arrange - XL Engine spanning multiple locations
        // Note: Gyro occupies slots 3-6 in CenterTorso, so we use slots 7-10 for engine
        var equipment = new List<ComponentData>
        {
            new()
            {
                Type = MakaMekComponent.Engine,
                Assignments =
                [
                    new LocationSlotAssignment(PartLocation.CenterTorso, 7, 4), // slots 7,8,9,10 (4 slots)
                    new LocationSlotAssignment(PartLocation.LeftTorso, 2, 3), // slots 2,3,4 (3 slots)
                    new LocationSlotAssignment(PartLocation.RightTorso, 2, 3)
                ],
                SpecificData = new EngineStateData(EngineType.XLFusion, 160)
            }
        };
        var unitData = CreateDummyMechData(equipment, true);

        // Act
        var mech = _mechFactory.Create(unitData);

        // Assert - Engine should only appear once in GetAllComponents
        var engines = mech.GetAllComponents<Engine>().ToList();
        engines.Count.ShouldBe(1);
        
        var engine = engines[0];
        engine.SlotAssignments.Count.ShouldBe(3);
        engine.IsMounted.ShouldBeTrue();
        
        // Check that the engine is mounted across all three locations
        var centerTorso = mech.Parts[PartLocation.CenterTorso];
        var leftTorso = mech.Parts[PartLocation.LeftTorso];
        var rightTorso = mech.Parts[PartLocation.RightTorso];
        
        // Engine should be in every location
        centerTorso.GetComponents<Engine>().Count().ShouldBe(1);
        leftTorso.GetComponents<Engine>().Count().ShouldBe(1);
        rightTorso.GetComponents<Engine>().Count().ShouldBe(1);
        
        // The engine should be mounted to all locations
        engine.GetLocations().ShouldContain(PartLocation.CenterTorso);
        engine.GetLocations().ShouldContain(PartLocation.LeftTorso);
        engine.GetLocations().ShouldContain(PartLocation.RightTorso);
    }

    [Fact]
    public void Create_WithMultipleDistinctComponents_CreatesDistinctInstances()
    {
        // Arrange - Two Medium Lasers in different locations
        var equipment = new List<ComponentData>
        {
            new ComponentData
            {
                Type = MakaMekComponent.MediumLaser,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 1, 1)]
            },
            new ComponentData
            {
                Type = MakaMekComponent.MediumLaser,
                Assignments = [new LocationSlotAssignment(PartLocation.LeftArm, 1, 1)]
            }
        };
        var unitData = CreateDummyMechData(equipment);

        // Act
        var mech = _mechFactory.Create(unitData);

        // Assert
        var allLasers = mech.GetAllComponents<MediumLaser>().ToList();
        allLasers.Count.ShouldBe(2);
        
        // Verify they are distinct instances
        allLasers[0].ShouldNotBeSameAs(allLasers[1]);
        
        // Verify they are in different locations
        var rightArmLasers = mech.Parts[PartLocation.RightArm].GetComponents<MediumLaser>().ToList();
        var leftArmLasers = mech.Parts[PartLocation.LeftArm].GetComponents<MediumLaser>().ToList();
        
        rightArmLasers.Count.ShouldBe(1);
        leftArmLasers.Count.ShouldBe(1);
        rightArmLasers[0].ShouldNotBeSameAs(leftArmLasers[0]);
    }

    [Fact]
    public void Create_WithNoEquipment_CreatesBasicMech()
    {
        // Arrange
        var unitData = CreateDummyMechData();

        // Act
        var mech = _mechFactory.Create(unitData);

        // Assert
        mech.ShouldNotBeNull();
        mech.Name.ShouldBe("Locust LCT-1V");
        mech.Tonnage.ShouldBe(20);
        
        // Should have basic structure but no additional equipment
        var allComponents = mech.GetAllComponents<Component>().ToList();
        // Only structural components like Gyro should be present
        allComponents.ShouldNotBeEmpty(); // Gyro and other structural components
    }

    [Fact]
    public void Create_WithExactSlotPositions_PreservesPositions()
    {
        // Arrange - Component at specific non-zero starting slot
        var equipment = new List<ComponentData>
        {
            new ComponentData
            {
                Type = MakaMekComponent.MediumLaser,
                Assignments = [new LocationSlotAssignment(PartLocation.RightArm, 2, 1)]
            }
        };
        var unitData = CreateDummyMechData(equipment);

        // Act
        var mech = _mechFactory.Create(unitData);

        // Assert
        var rightArm = mech.Parts[PartLocation.RightArm];
        var laser = rightArm.GetComponents<MediumLaser>().First();

        laser.MountedAtFirstLocationSlots.ShouldBe([2]);
        rightArm.GetComponentAtSlot(0).ShouldNotBeNull(); // ShoulderActuator is at slot 0
        rightArm.GetComponentAtSlot(1).ShouldBeNull();
        rightArm.GetComponentAtSlot(2).ShouldBe(laser);
        rightArm.GetComponentAtSlot(3).ShouldBeNull();
    }
}
