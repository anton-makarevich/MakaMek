using Sanet.MakaMek.Core.Events;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Map;

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
        var torso = new TestTorso("Test Torso", PartLocation.LeftTorso, maxArmor, maxRearArmor, maxStructure);

        // Act
        var excessDamage = torso.ApplyDamage(damage, direction);

        // Assert
        excessDamage.ShouldBe(expectedExcess);

        if (direction == HitDirection.Rear)
        {
            if (damage <= maxRearArmor)
            {
                torso.CurrentRearArmor.ShouldBe(maxRearArmor - damage);
                torso.CurrentArmor.ShouldBe(maxArmor);
                torso.CurrentStructure.ShouldBe(maxStructure);
            }
            else
            {
                torso.CurrentRearArmor.ShouldBe(0);
                var remainingDamage = damage - maxRearArmor;
                if (remainingDamage < maxStructure)
                {
                    torso.CurrentStructure.ShouldBe(maxStructure - remainingDamage);
                }
                else
                {
                    torso.CurrentStructure.ShouldBe(0);
                }
            }
        }
        else // Rear
        {
            if (damage <= maxArmor)
            {
                torso.CurrentArmor.ShouldBe(maxArmor - damage);
                torso.CurrentRearArmor.ShouldBe(maxRearArmor);
                torso.CurrentStructure.ShouldBe(maxStructure);
            }
            else
            {
                torso.CurrentArmor.ShouldBe(0);
                var remainingDamage = damage - maxArmor;
                if (remainingDamage < maxStructure)
                {
                    torso.CurrentStructure.ShouldBe(maxStructure - remainingDamage);
                }
                else
                {
                    torso.CurrentStructure.ShouldBe(0);
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
        var torso = new TestTorso("Test Torso", PartLocation.CenterTorso, maxArmor, maxRearArmor, maxStructure);

        // Act
        var excessDamage = torso.ApplyDamage(damage, HitDirection.Rear);

        // Assert
        excessDamage.ShouldBe(expectedExcess);

        if (damage <= maxRearArmor)
        {
            torso.CurrentRearArmor.ShouldBe(maxRearArmor - damage);
            torso.CurrentStructure.ShouldBe(maxStructure);
        }
        else if (damage < maxRearArmor + maxStructure)
        {
            torso.CurrentRearArmor.ShouldBe(0);
            torso.CurrentStructure.ShouldBe(maxStructure - (damage - maxRearArmor));
        }
        // Rear hits must not affect front armor
        torso.CurrentArmor.ShouldBe(maxArmor);
    }

    [Fact]
    public void Rotate_ShouldSetNewFacing()
    {
        // Arrange
        var torso = new TestTorso("Test Torso", PartLocation.LeftTorso, 10, 3, 5);
        
        // Act
        torso.Rotate(HexDirection.TopRight);

        // Assert
        torso.Facing.ShouldBe(HexDirection.TopRight);
    }

    [Fact]
    public void ResetRotation_WhenUnitNotSet_ShouldNotThrow()
    {
        // Arrange
        var torso = new TestTorso("Test Torso", PartLocation.LeftTorso, 10, 3, 5);
        
        // Act & Assert
        Should.NotThrow(() => torso.ResetRotation());
    }

    [Fact]
    public void ResetRotation_WhenUnitSet_ShouldMatchUnitFacing()
    {
        // Arrange
        var torso = new TestTorso("Test Torso", PartLocation.LeftTorso, 10, 3, 5);
        var mech = new Mech("Test", "TST-1A", 4, new List<UnitPart> { torso });
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        mech.Deploy(position);
        
        // Set different rotation
        torso.Rotate(HexDirection.Bottom);
        torso.Facing.ShouldBe(HexDirection.Bottom, "Torso should be rotated before reset");

        // Act
        torso.ResetRotation();

        // Assert
        torso.Facing.ShouldBe(HexDirection.TopRight, "Torso should match unit facing after reset");
    }

    [Fact]
    public void BlowOff_ShouldReturnFalse()
    {
        var torso = new TestTorso("Test Torso", PartLocation.LeftTorso, 10, 3, 5);
        
        // Act
        var isBlownOff = torso.BlowOff();
        
        // Assert
        isBlownOff.ShouldBeFalse();
        torso.IsBlownOff.ShouldBeFalse();
    }
    
    [Fact]
    public void ApplyDamage_RaisesArmorDamageEvent()
    {
        // Arrange
        var unit = new UnitPartTests.TestUnit();
        var torso = new TestTorso("Test Torso", PartLocation.CenterTorso, 10, 10, 10)
            {
                Unit = unit
            };

        // Act
        torso.ApplyDamage(5, HitDirection.Rear);

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
        var torso = new TestTorso("Test Torso", PartLocation.CenterTorso, 10, 10, 10)
        {
            Unit = unit
        };

        // Act
        torso.ApplyDamage(15, HitDirection.Rear);

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
        var torso = new TestTorso("Test Torso", PartLocation.CenterTorso, 10, 10, 10);

        // Act
        torso.ApplyDamage(1, HitDirection.Rear);

        // Assert
        torso.IsPristine.ShouldBeFalse();
    }
    
    [Fact]
    public void ToData_ShouldIncludeRearArmor()
    {
        // Arrange
        var torso = new TestTorso("Test Torso", PartLocation.CenterTorso, 10, 10, 10);
        torso.ApplyDamage(5, HitDirection.Rear);
        
        // Act
        var data = torso.ToData();
        
        // Assert
        data.CurrentRearArmor.ShouldBe(5);
    }
}