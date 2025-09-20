using Shouldly;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Tests.Data.Community;

public class MtfDataProviderTests
{
    private readonly string[] _locustMtfData = File.ReadAllLines("Resources/Mechs/LCT-1V.mtf");

    [Fact]
    public void Parse_LocustMtf_ReturnsCorrectBasicData()
    {
        // Arrange
        var parser = new MtfDataProvider();

        // Act
        var mechData = parser.LoadMechFromTextData(_locustMtfData);

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
        var parser = new MtfDataProvider();

        // Act
        var mechData = parser.LoadMechFromTextData(_locustMtfData);

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
        var parser = new MtfDataProvider();

        // Act
        var mechData = parser.LoadMechFromTextData(_locustMtfData);

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
        var parser = new MtfDataProvider();

        // Act
        var mechData = parser.LoadMechFromTextData(_locustMtfData);

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
        var parser = new MtfDataProvider();

        // Act
        var mechData = parser.LoadMechFromTextData(_locustMtfData);

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
}
