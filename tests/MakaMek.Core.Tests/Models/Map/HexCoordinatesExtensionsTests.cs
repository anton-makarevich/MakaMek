using NSubstitute;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
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
    
    [Fact]
    public void IsOccupied_ReturnsTrue_WhenUnitDeployedInHex()
    {
        // Arrange
        var sut = new HexCoordinates(5, 5);
        
        var testUnit = new Mech("Test", "TST-1A", 50, []);
        testUnit.Deploy(new HexPosition(sut, HexDirection.Top));
        var player = Substitute.For<IPlayer>();
        player.Units.Returns([testUnit]);
        var game = Substitute.For<IGame>();
        game.Players.Returns([player]);
        
        // Act
        var result = sut.IsOccupied(game);
        
        // Assert
        result.ShouldBeTrue();
    }
    
    [Fact]
    public void IsOccupied_ReturnsFalse_WhenNoUnitDeployedInHex()
    {
        // Arrange
        var sut = new HexCoordinates(5, 5);
        
        var testUnit = new Mech("Test", "TST-1A", 50, []);
        // Deploy the unit in a different hex
        testUnit.Deploy(new HexPosition(new HexCoordinates(4, 4), HexDirection.Top));
        var player = Substitute.For<IPlayer>();
        player.Units.Returns([testUnit]);
        var game = Substitute.For<IGame>();
        game.Players.Returns([player]);
        
        // Act
        var result = sut.IsOccupied(game);
        
        // Assert
        result.ShouldBeFalse();
    }
}