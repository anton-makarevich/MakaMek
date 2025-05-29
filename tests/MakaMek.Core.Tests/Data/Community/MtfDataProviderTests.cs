using Shouldly;
using Sanet.MakaMek.Core.Data.Community;
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
    public void Parse_LocustMtf_ReturnsCorrectEquipment()
    {
        // Arrange
        var parser = new MtfDataProvider();

        // Act
        var mechData = parser.LoadMechFromTextData(_locustMtfData);

        // Assert
        // Left Arm
        var leftArmEquipment = mechData.LocationEquipment[PartLocation.LeftArm];
        leftArmEquipment.ShouldContain(MakaMekComponent.Shoulder);
        leftArmEquipment.ShouldContain(MakaMekComponent.UpperArmActuator);
        leftArmEquipment.ShouldContain(MakaMekComponent.MachineGun);

        // Right Arm
        var rightArmEquipment = mechData.LocationEquipment[PartLocation.RightArm];
        rightArmEquipment.ShouldContain(MakaMekComponent.Shoulder);
        rightArmEquipment.ShouldContain(MakaMekComponent.UpperArmActuator);
        rightArmEquipment.ShouldContain(MakaMekComponent.MachineGun);

        // Center Torso
        var centerTorsoEquipment = mechData.LocationEquipment[PartLocation.CenterTorso];
        centerTorsoEquipment.ShouldContain(MakaMekComponent.Engine);
        centerTorsoEquipment.ShouldContain(MakaMekComponent.Gyro);
        centerTorsoEquipment.ShouldContain(MakaMekComponent.MediumLaser);
    }
}
