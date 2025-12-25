using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Map;

public class HexCoordinatesExtensionsTests
{
    [Fact]
    public void IsInWeaponFiringArc_ReturnsTrueForForwardArc_WhenMountedOnArm()
    {
        // Arrange
        var center = new HexCoordinates(5, 5);
        var target = new HexCoordinates(4, 4);
        
        var weapon = new MediumLaser();
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        arm.TryAddComponent(weapon).ShouldBeTrue();
        
        // Act
        var result = center.IsInWeaponFiringArc(target, weapon, HexDirection.Top);
        
        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void IsInWeaponFiringArc_ReturnsTrueForLeftArc_WhenMountedOnLeftArm()
    {
        // Arrange
        var center = new HexCoordinates(5, 5);
        var target = new HexCoordinates(3, 5);
        
        var weapon = new MediumLaser();
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        arm.TryAddComponent(weapon).ShouldBeTrue();
        
        // Act
        var result = center.IsInWeaponFiringArc(target, weapon, HexDirection.Top);
        
        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void IsInWeaponFiringArc_ReturnsFalseForRightArc_WhenMountedOnLeftArm()
    {
        // Arrange
        var center = new HexCoordinates(5, 5);
        var target = new HexCoordinates(7, 5);
        
        var weapon = new MediumLaser();
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        arm.TryAddComponent(weapon).ShouldBeTrue();
        
        // Act
        var result = center.IsInWeaponFiringArc(target, weapon, HexDirection.Top);
        
        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsInWeaponFiringArc_ReturnsFalseForRearArc_WhenMountedOnLeftArm()
    {
        // Arrange
        var center = new HexCoordinates(5, 5);
        var target = new HexCoordinates(5, 6);
        
        var weapon = new MediumLaser();
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        arm.TryAddComponent(weapon).ShouldBeTrue();
        
        // Act
        var result = center.IsInWeaponFiringArc(target, weapon, HexDirection.Top);
        
        // Assert
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void IsInWeaponFiringArc_ReturnsFalseForForwardArc_WhenNotDeployed_AndNoFacingOverride()
    {
        // Arrange
        var center = new HexCoordinates(5, 5);
        var target = new HexCoordinates(4, 4);
        
        var weapon = new MediumLaser();
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        arm.TryAddComponent(weapon).ShouldBeTrue();
        
        // Act
        var result = center.IsInWeaponFiringArc(target, weapon);
        
        // Assert
        result.ShouldBeFalse();
    }
    
    [Fact]
    public void IsInWeaponFiringArc_ReturnsTrueForForwardArc_WhenUnitDeployed_AndNoFacingOverride()
    {
        // Arrange
        var center = new HexCoordinates(5, 5);
        var target = new HexCoordinates(4, 4);
        
        var weapon = new MediumLaser();
        var arm = new Arm("Arm", PartLocation.LeftArm, 4, 3);
        arm.TryAddComponent(weapon).ShouldBeTrue();
        
        var testUnit = new Mech("Test", "TST-1A", 50, [arm]);
        testUnit.Deploy(new HexPosition(center, HexDirection.Top));
        
        // Act
        var result = center.IsInWeaponFiringArc(target, weapon);
        
        // Assert
        result.ShouldBeTrue();
    }
}