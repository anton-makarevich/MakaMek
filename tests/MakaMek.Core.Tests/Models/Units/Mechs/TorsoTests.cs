using Sanet.MakaMek.Core.Events;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class TorsoTests
{
    private class TestTorso(string name, PartLocation location, int maxArmor, int maxRearArmor, int maxStructure)
        : Torso(name, location, maxArmor, maxRearArmor, maxStructure);

    [Theory]
    [InlineData(5, 10, 3, 5, 0, HitDirection.Front)] // Front damage less than armor
    [InlineData(5, 10, 3, 5, 0, HitDirection.Left)] // Side considered front
    [InlineData(5, 10, 3, 5, 0, HitDirection.Right)] // Side considered front
    [InlineData(3, 10, 3, 5, 0, HitDirection.Rear)] // Rear damage less than rear armor
    [InlineData(14, 10, 3, 5, 0, HitDirection.Front)] // Front damage depletes armor and some structure
    [InlineData(9, 10, 3, 5, 1, HitDirection.Rear)] // Rear damage exceeds rear armor and depletes structure
    public void ApplyDamage_HandlesVariousDamageScenarios(int damage,
        int maxArmor,
        int maxRearArmor,
        int maxStructure,
        int expectedExcess,
        HitDirection direction)
    {
        // Arrange
        var sut = new TestTorso("Test Torso", PartLocation.LeftTorso, maxArmor, maxRearArmor, maxStructure);

        // Act
        var excessDamage = sut.ApplyDamage(damage, direction);

        // Assert
        excessDamage.ShouldBe(expectedExcess);

        if (direction == HitDirection.Rear)
        {
            if (damage <= maxRearArmor)
            {
                sut.CurrentRearArmor.ShouldBe(maxRearArmor - damage);
                sut.CurrentArmor.ShouldBe(maxArmor);
                sut.CurrentStructure.ShouldBe(maxStructure);
            }
            else
            {
                sut.CurrentRearArmor.ShouldBe(0);
                var remainingDamage = damage - maxRearArmor;
                if (remainingDamage < maxStructure)
                {
                    sut.CurrentStructure.ShouldBe(maxStructure - remainingDamage);
                }
                else
                {
                    sut.CurrentStructure.ShouldBe(0);
                }
            }
        }
        else
        {
            if (damage <= maxArmor)
            {
                sut.CurrentArmor.ShouldBe(maxArmor - damage);
                sut.CurrentRearArmor.ShouldBe(maxRearArmor);
                sut.CurrentStructure.ShouldBe(maxStructure);
            }
            else
            {
                sut.CurrentArmor.ShouldBe(0);
                var remainingDamage = damage - maxArmor;
                if (remainingDamage < maxStructure)
                {
                    sut.CurrentStructure.ShouldBe(maxStructure - remainingDamage);
                }
                else
                {
                    sut.CurrentStructure.ShouldBe(0);
                }
            }
        }
    }

    [Theory]
    [InlineData(5, 10, 5, 5, 0)] // Damage does not exceed rear armor
    [InlineData(15, 10, 5, 5,5)] // Damage exceeds rear armor
    [InlineData(8, 10, 5, 5,0)] // Damage exceeds rear armor but structure remains
    [InlineData(10, 10, 5, 5,0)] // Damage exactly equals rear armor + structure (boundary)
    public void ApplyDamage_HandlesRearArmor(int damage, int maxArmor, int maxRearArmor, int maxStructure, int expectedExcess)
    {
        // Arrange
        var sut = new TestTorso("Test Torso", PartLocation.CenterTorso, maxArmor, maxRearArmor, maxStructure);

        // Act
        var excessDamage = sut.ApplyDamage(damage, HitDirection.Rear);

        // Assert
        excessDamage.ShouldBe(expectedExcess);

        if (damage <= maxRearArmor)
        {
            sut.CurrentRearArmor.ShouldBe(maxRearArmor - damage);
            sut.CurrentStructure.ShouldBe(maxStructure);
        }
        else if (damage < maxRearArmor + maxStructure)
        {
            sut.CurrentRearArmor.ShouldBe(0);
            sut.CurrentStructure.ShouldBe(maxStructure - (damage - maxRearArmor));
        }
        // Rear hits must not affect front armor
        sut.CurrentArmor.ShouldBe(maxArmor);
    }

    [Fact]
    public void Rotate_ShouldSetNewFacing()
    {
        // Arrange
        var sut = new TestTorso("Test Torso", PartLocation.LeftTorso, 10, 3, 5);
        
        // Act
        sut.Rotate(HexDirection.TopRight);

        // Assert
        sut.Facing.ShouldBe(HexDirection.TopRight);
    }

    [Fact]
    public void ResetRotation_WhenUnitNotSet_ShouldNotThrow()
    {
        // Arrange
        var sut = new TestTorso("Test Torso", PartLocation.LeftTorso, 10, 3, 5);
        
        // Act & Assert
        Should.NotThrow(() => sut.ResetRotation());
    }

    [Fact]
    public void ResetRotation_WhenUnitSet_ShouldMatchUnitFacing()
    {
        // Arrange
        var sut = new TestTorso("Test Torso", PartLocation.LeftTorso, 10, 3, 5);
        var mech = new Mech("Test", "TST-1A", 4, [sut]);
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        mech.Deploy(position);
        
        // Set different rotation
        sut.Rotate(HexDirection.Bottom);
        sut.Facing.ShouldBe(HexDirection.Bottom, "Torso should be rotated before reset");

        // Act
        sut.ResetRotation();

        // Assert
        sut.Facing.ShouldBe(HexDirection.TopRight, "Torso should match unit facing after reset");
    }

    [Fact]
    public void BlowOff_ShouldReturnFalse()
    {
        var sut = new TestTorso("Test Torso", PartLocation.LeftTorso, 10, 3, 5);
        
        // Act
        var isBlownOff = sut.BlowOff();
        
        // Assert
        isBlownOff.ShouldBeFalse();
        sut.IsBlownOff.ShouldBeFalse();
    }
    
    [Fact]
    public void ApplyDamage_RaisesArmorDamageEvent()
    {
        // Arrange
        var unit = new UnitPartTests.TestUnit();
        var sut = new TestTorso("Test Torso", PartLocation.CenterTorso, 10, 10, 10)
            {
                Unit = unit
            };

        // Act
        sut.ApplyDamage(5, HitDirection.Rear);

        // Assert
        var uiEvent = unit.DequeueNotification();
        uiEvent.ShouldNotBeNull();
        uiEvent.Type.ShouldBe(UiEventType.ArmorDamage);
        uiEvent.Parameters.Length.ShouldBe(2);
        uiEvent.Parameters[0].ShouldBe("Test Torso");
        uiEvent.Parameters[1].ShouldBe("5");
    }
    
    [Fact]
    public void ApplyDamage_RaisesArmorDamageEvent_WhenArmorFullyDestroyed()
    {
        // Arrange
        var unit = new UnitPartTests.TestUnit();
        var sut = new TestTorso("Test Torso", PartLocation.CenterTorso, 10, 10, 10)
        {
            Unit = unit
        };

        // Act
        sut.ApplyDamage(15, HitDirection.Rear);

        // Assert
        unit.Notifications.Count.ShouldBe(2); //10 armor damage + 5 structure damage
        var uiEvent = unit.DequeueNotification();
        uiEvent.ShouldNotBeNull();
        uiEvent.Type.ShouldBe(UiEventType.ArmorDamage);
        uiEvent.Parameters.Length.ShouldBe(2);
        uiEvent.Parameters[0].ShouldBe("Test Torso");
        uiEvent.Parameters[1].ShouldBe("10");
    }
    
    [Fact]
    public void ApplyDamage_WithRearArmor_ShouldSetIsPristineToFalse()
    {
        // Arrange
        var sut = new TestTorso("Test Torso", PartLocation.CenterTorso, 10, 10, 10);

        // Act
        sut.ApplyDamage(1, HitDirection.Rear);

        // Assert
        sut.IsPristine.ShouldBeFalse();
    }
    
    [Fact]
    public void ToData_ShouldIncludeRearArmor()
    {
        // Arrange
        var sut = new TestTorso("Test Torso", PartLocation.CenterTorso, 10, 10, 10);
        sut.ApplyDamage(5, HitDirection.Rear);
        
        // Act
        var data = sut.ToData();
        
        // Assert
        data.CurrentRearArmor.ShouldBe(5);
    }
    
    [Theory]
    [InlineData(MountingOptions.Standard, FiringArc.Front)]
    [InlineData(MountingOptions.Rear, FiringArc.Rear)]
    public void GetFiringArcs_ShouldReturnCorrectArcs(MountingOptions mountingOptions, FiringArc expectedArc)
    {
        var sut = new TestTorso("Test Torso", PartLocation.CenterTorso, 10, 10, 10);
        
        sut.GetFiringArcs(mountingOptions).ShouldBe([expectedArc]);
    }

    [Fact]
    public void GetWeaponsConfigurationOptions_WhenNotDeployed_ShouldBeEmpty()
    {
        var torso = new TestTorso("Torso", PartLocation.CenterTorso, 10, 10, 10);
        var mech = new Mech("Test", "TST-1A", 50, [torso]);

        mech.IsDeployed.ShouldBeFalse();

        torso.GetWeaponsConfigurationOptions().ShouldBeEmpty();
    }

    [Fact]
    public void GetWeaponsConfigurationOptions_WhenDeployedAndCanRotateTorso_ShouldReturnTorsoRotationOptions()
    {
        var torso = new TestTorso("Torso", PartLocation.CenterTorso, 10, 10, 10);
        var mech = new Mech("Test", "TST-1A", 50, [torso], possibleTorsoRotation: 1);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        var options = torso.GetWeaponsConfigurationOptions();

        options.Count.ShouldBe(1);
        options[0].Type.ShouldBe(WeaponConfigurationType.TorsoRotation);
        options[0].AvailableDirections.ShouldBe([HexDirection.TopRight, HexDirection.TopLeft]);
    }
}