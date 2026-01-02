using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class ArmTests
{
    [Theory]
    [InlineData(PartLocation.LeftArm)]
    [InlineData(PartLocation.RightArm)]
    public void Arm_ShouldBeInitializedCorrectly(PartLocation location)
    {
        var sut = new Arm("Arm", location, 4, 3);

        sut.Location.ShouldBe(location);
        sut.MaxArmor.ShouldBe(4);
        sut.MaxStructure.ShouldBe(3);
        sut.CanBeBlownOff.ShouldBeTrue();
        sut.TotalSlots.ShouldBe(12);
    }
    
    [Fact]
    public void Facing_ShouldBeNull_WhenNotDeployed()
    {
        var sut = new Arm("Left Arm", PartLocation.LeftArm, 4, 3);
        sut.Facing.ShouldBeNull();
    }
    
    [Fact]
    public void Facing_ShouldMatchUnitFacing_WhenDeployed()
    {
        var sut = new Arm("Left Arm", PartLocation.LeftArm, 4, 3);
        var mech = new Mech("Test", "TST-1A", 4, [sut]);
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        mech.Deploy(position);
        
        sut.Facing.ShouldBe(position.Facing);
    }
    
    [Fact]
    public void Facing_ShouldChange_WithTorso_WhenTorsoRotated()
    {
        var leftArm = new Arm("Left Arm", PartLocation.LeftArm, 4, 3);
        var rightArm = new Arm("Right Arm", PartLocation.RightArm, 4, 3);
        var leftTorso = new SideTorso("Left Torso", PartLocation.LeftTorso, 10, 5, 10);
        var rightTorso = new SideTorso("Right Torso", PartLocation.RightTorso, 10, 5, 10);
        var centerTorso = new CenterTorso("Center Torso", 15, 10, 15);
        var mech = new Mech("Test", "TST-1A", 4, [leftArm, rightArm, leftTorso, rightTorso, centerTorso]);
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        mech.Deploy(position);
        
        leftArm.Facing.ShouldBe(position.Facing);
        rightArm.Facing.ShouldBe(position.Facing);
        
        mech.RotateTorso(HexDirection.TopRight);
        
        leftArm.Facing.ShouldBe(HexDirection.TopRight);
        rightArm.Facing.ShouldBe(HexDirection.TopRight);
    }
    
    [Theory]
    [InlineData(PartLocation.LeftArm, FiringArc.Left)]
    [InlineData(PartLocation.RightArm, FiringArc.Right)]
    public void GetFiringArcs_ShouldIncludeCorrectSideArc(PartLocation location, FiringArc sideArc)
    {
        var sut = new Arm("Arm", location, 4, 3);
        
        sut.GetFiringArcs(MountingOptions.Standard).ShouldBe([FiringArc.Front, sideArc]);
    }
    
    [Theory]
    [InlineData(MountingOptions.Standard)]
    [InlineData(MountingOptions.Rear)]
    public void GetFiringArcs_ShouldIgnoreMountingOptions(MountingOptions mountingOptions)
    {
        var sut = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        
        sut.GetFiringArcs(mountingOptions).ShouldContain(FiringArc.Front);
    }

    [Fact]
    public void GetWeaponsConfigurationOptions_WhenNotDeployed_ShouldBeEmpty()
    {
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        var mech = new Mech("Test", "TST-1A", 50, [arm], possibleTorsoRotation: 1);

        mech.IsDeployed.ShouldBeFalse();

        arm.GetWeaponsConfigurationOptions().ShouldBeEmpty();
    }

    [Fact]
    public void GetWeaponsConfigurationOptions_WhenDeployedAndCanRotateTorso_ShouldIncludeTorsoRotation()
    {
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        var mech = new Mech("Test", "TST-1A", 50, [arm], possibleTorsoRotation: 1);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        var options = arm.GetWeaponsConfigurationOptions();

        options.Count.ShouldBe(1);
        options[0].Type.ShouldBe(WeaponConfigurationType.TorsoRotation);
        options[0].AvailableDirections.ShouldBe([HexDirection.TopRight, HexDirection.TopLeft]);
    }
    
    [Fact]
    public void GetWeaponsConfigurationOptions_WhenDeployedAndCanFlipArms_ShouldIncludeArmsFlip()
    {
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        var mech = new Mech("Test", "TST-1A", 50, [arm], canFlipArms: true);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        var options = arm.GetWeaponsConfigurationOptions();

        options.Count.ShouldBe(2);
        options[1].Type.ShouldBe(WeaponConfigurationType.ArmsFlip);
        options[1].AvailableDirections.ShouldBe([HexDirection.Bottom]);
    }
    
    [Fact]
    public void IsWeaponConfigurationApplicable_ShouldReturnTrueForTorsoRotation_WhenDeployed()
    {
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        var mech = new Mech("Test", "TST-1A", 50, [arm], possibleTorsoRotation: 1);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));
        
        arm.IsWeaponConfigurationApplicable(WeaponConfigurationType.TorsoRotation).ShouldBeTrue();
    }
    
    [Fact]
    public void IsWeaponConfigurationApplicable_ShouldReturnFalseForTorsoRotation_WhenNotDeployed()
    {
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        
        arm.IsWeaponConfigurationApplicable(WeaponConfigurationType.TorsoRotation).ShouldBeFalse();
    }
    
    [Fact]
    public void IsWeaponConfigurationApplicable_ShouldReturnTrueForArmsFlip_WhenDeployedAndCanFlipArms()
    {
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        var mech = new Mech("Test", "TST-1A", 50, [arm], canFlipArms: true);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));
        
        arm.IsWeaponConfigurationApplicable(WeaponConfigurationType.ArmsFlip).ShouldBeTrue();
    }
}