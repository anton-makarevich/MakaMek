using Shouldly;
using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
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
        var leftArmLayout = mechData.LocationEquipment[PartLocation.LeftArm];
        leftArmLayout.GetComponentAtSlot(0).ShouldBe(MakaMekComponent.Shoulder);
        leftArmLayout.GetComponentAtSlot(1).ShouldBe(MakaMekComponent.UpperArmActuator);
        leftArmLayout.GetComponentAtSlot(2).ShouldBe(MakaMekComponent.MachineGun);
        leftArmLayout.GetComponentAtSlot(3).ShouldBeNull(); // Empty slot

        // Right Arm - verify exact slot positions
        var rightArmLayout = mechData.LocationEquipment[PartLocation.RightArm];
        rightArmLayout.GetComponentAtSlot(0).ShouldBe(MakaMekComponent.Shoulder);
        rightArmLayout.GetComponentAtSlot(1).ShouldBe(MakaMekComponent.UpperArmActuator);
        rightArmLayout.GetComponentAtSlot(2).ShouldBe(MakaMekComponent.MachineGun);
        rightArmLayout.GetComponentAtSlot(3).ShouldBeNull(); // Empty slot

        // Center Torso - verify exact slot positions based on MTF file
        var centerTorsoLayout = mechData.LocationEquipment[PartLocation.CenterTorso];
        centerTorsoLayout.GetComponentAtSlot(0).ShouldBe(MakaMekComponent.Engine);
        centerTorsoLayout.GetComponentAtSlot(1).ShouldBe(MakaMekComponent.Engine);
        centerTorsoLayout.GetComponentAtSlot(2).ShouldBe(MakaMekComponent.Engine);
        centerTorsoLayout.GetComponentAtSlot(3).ShouldBe(MakaMekComponent.Gyro);
        centerTorsoLayout.GetComponentAtSlot(4).ShouldBe(MakaMekComponent.Gyro);
        centerTorsoLayout.GetComponentAtSlot(5).ShouldBe(MakaMekComponent.Gyro);
        centerTorsoLayout.GetComponentAtSlot(6).ShouldBe(MakaMekComponent.Gyro);
        centerTorsoLayout.GetComponentAtSlot(7).ShouldBe(MakaMekComponent.Engine);
        centerTorsoLayout.GetComponentAtSlot(8).ShouldBe(MakaMekComponent.Engine);
        centerTorsoLayout.GetComponentAtSlot(9).ShouldBe(MakaMekComponent.Engine);
        centerTorsoLayout.GetComponentAtSlot(10).ShouldBe(MakaMekComponent.MediumLaser);
        centerTorsoLayout.GetComponentAtSlot(11).ShouldBe(MakaMekComponent.ISAmmoMG);
    }

    [Fact]
    public void Parse_LocustMtf_ReturnsCorrectComponentAssignments()
    {
        // Arrange
        var parser = new MtfDataProvider();

        // Act
        var mechData = parser.LoadMechFromTextData(_locustMtfData);

        // Assert
        var centerTorsoLayout = mechData.LocationEquipment[PartLocation.CenterTorso];
        var assignments = centerTorsoLayout.ComponentAssignments;

        // Engine should occupy slots 0-2 and 7-9 (6 slots total, in two separate assignments due to non-consecutive slots)
        var engineAssignments = assignments.Where(a => a.Component == MakaMekComponent.Engine).ToList();
        engineAssignments.Count.ShouldBe(2);

        var firstEngineAssignment = engineAssignments.FirstOrDefault(a => a.Slots.Contains(0));
        firstEngineAssignment.ShouldNotBeNull();
        firstEngineAssignment.Slots.ShouldBe(new[] { 0, 1, 2 });

        var secondEngineAssignment = engineAssignments.FirstOrDefault(a => a.Slots.Contains(7));
        secondEngineAssignment.ShouldNotBeNull();
        secondEngineAssignment.Slots.ShouldBe(new[] { 7, 8, 9 });

        // Gyro should occupy slots 3-6 (4 slots total)
        var gyroAssignment = assignments.FirstOrDefault(a => a.Component == MakaMekComponent.Gyro);
        gyroAssignment.ShouldNotBeNull();
        gyroAssignment.Slots.ShouldBe(new[] { 3, 4, 5, 6 });

        // Medium Laser should occupy slot 10 (1 slot)
        var laserAssignment = assignments.FirstOrDefault(a => a.Component == MakaMekComponent.MediumLaser);
        laserAssignment.ShouldNotBeNull();
        laserAssignment.Slots.ShouldBe(new[] { 10 });

        // Ammo should occupy slot 11 (1 slot)
        var ammoAssignment = assignments.FirstOrDefault(a => a.Component == MakaMekComponent.ISAmmoMG);
        ammoAssignment.ShouldNotBeNull();
        ammoAssignment.Slots.ShouldBe(new[] { 11 });
    }

    [Fact]
    public void Parse_LocustMtf_HandlesEmptySlots()
    {
        // Arrange
        var parser = new MtfDataProvider();

        // Act
        var mechData = parser.LoadMechFromTextData(_locustMtfData);

        // Assert
        var leftTorsoLayout = mechData.LocationEquipment[PartLocation.LeftTorso];

        // Left Torso should be completely empty (all slots null)
        for (int slot = 0; slot < 12; slot++)
        {
            leftTorsoLayout.GetComponentAtSlot(slot).ShouldBeNull($"Slot {slot} should be empty");
        }

        leftTorsoLayout.OccupiedSlotCount.ShouldBe(0);
        leftTorsoLayout.ComponentAssignments.Count.ShouldBe(0);
    }

    [Fact]
    public void Parse_LocustMtf_VerifiesHeadSlotLayout()
    {
        // Arrange
        var parser = new MtfDataProvider();

        // Act
        var mechData = parser.LoadMechFromTextData(_locustMtfData);

        // Assert
        var headLayout = mechData.LocationEquipment[PartLocation.Head];

        // Based on the MTF file structure
        headLayout.GetComponentAtSlot(0).ShouldBe(MakaMekComponent.LifeSupport);
        headLayout.GetComponentAtSlot(1).ShouldBe(MakaMekComponent.Sensors);
        headLayout.GetComponentAtSlot(2).ShouldBe(MakaMekComponent.Cockpit);
        headLayout.GetComponentAtSlot(3).ShouldBeNull(); // Empty
        headLayout.GetComponentAtSlot(4).ShouldBe(MakaMekComponent.Sensors);
        headLayout.GetComponentAtSlot(5).ShouldBe(MakaMekComponent.LifeSupport);

        // Slots 6-11 should be empty
        for (int slot = 6; slot < 12; slot++)
        {
            headLayout.GetComponentAtSlot(slot).ShouldBeNull($"Head slot {slot} should be empty");
        }
    }
}
