using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Models.Units.Pilots;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Mechs;

public class HeadTests
{
    [Fact]
    public void Head_ShouldBeInitializedCorrectly()
    {
        // Arrange & Act
        var sut = new Head("Head",  8, 3);

        // Assert
        sut.Name.ShouldBe("Head");
        sut.Location.ShouldBe(PartLocation.Head);
        sut.MaxArmor.ShouldBe(8);
        sut.CurrentArmor.ShouldBe(8);
        sut.MaxStructure.ShouldBe(3);
        sut.CurrentStructure.ShouldBe(3);
        sut.TotalSlots.ShouldBe(6);
        sut.CanBeBlownOff.ShouldBeTrue();

        // Verify default components
        sut.GetComponent<LifeSupport>().ShouldNotBeNull();
        sut.GetComponent<Sensors>().ShouldNotBeNull();
        sut.GetComponent<Cockpit>().ShouldNotBeNull();
    }
    
    [Fact]
    public void BlowOff_ShouldKillPilot()
    {
        // Arrange
        var sut = new Head("Head",  8, 3);
        var mech = new Mech("Test", "TST-1A", 50, [sut]);
        var pilot = new MechWarrior("John", "Doe");
        mech.AssignPilot(pilot);

        // Act
        sut.BlowOff();

        // Assert
        pilot.IsDead.ShouldBeTrue();
    }
    
    [Fact]
    public void BlowOff_ShouldNotThrow_WhenPilotIsNotAssigned()
    {
        // Arrange
        var sut = new Head("Head",  8, 3);

        // Act & Assert
        sut.BlowOff();
    }
    
    [Fact]
    public void ApplyDamage_ShouldHitPilot()
    {
        // Arrange
        var sut = new Head("Head",  8, 3);
        var mech = new Mech("Test", "TST-1A", 50, [sut]);
        var pilot = new MechWarrior("John", "Doe");
        mech.AssignPilot(pilot);

        // Act
        sut.ApplyDamage(1, HitDirection.Front);

        // Assert
        pilot.Injuries.ShouldBe(1);
    }
    
    [Fact]
    public void ApplyDamage_ShouldNoHitPilot_WhenPilotIsNotAssigned()
    {
        // Arrange
        var sut = new Head("Head",  8, 3);
        var pilot = new MechWarrior("John", "Doe");

        // Act
        sut.ApplyDamage(1, HitDirection.Front);

        // Assert
        pilot.Injuries.ShouldBe(0);
    }
    
    [Fact]
    public void ApplyDamage_ShouldNotHitPilot_WhenDamageIsZero()
    {
        // Arrange
        var sut = new Head("Head",  8, 3);
        var mech = new Mech("Test", "TST-1A", 50, [sut]);
        var pilot = new MechWarrior("John", "Doe");
        mech.AssignPilot(pilot);

        // Act
        sut.ApplyDamage(0, HitDirection.Front);

        // Assert
        pilot.Injuries.ShouldBe(0);
    }
    
    [Fact]
    public void Facing_ShouldBeNull_WhenNotDeployed()
    {
        var sut = new Head("Head",  8, 3);
        
        sut.Facing.ShouldBeNull();
    }
    
    [Fact]
    public void Facing_ShouldMatchUnitFacing_WhenDeployed()
    {
        var sut = new Head("Head",  8, 3);
        var mech = new Mech("Test", "TST-1A", 4, [sut]);
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        mech.Deploy(position);
        
        sut.Facing.ShouldBe(position.Facing);
    }
    
    [Fact]
    public void Facing_ShouldChange_WhenTorsoIsRotated()
    {
        var sut = new Head("Head",  8, 3);
        var torso = new CenterTorso("CenterTorso", 10, 2, 6);
        var mech = new Mech("Test", "TST-1A", 4, [sut, torso]);
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.TopRight);
        mech.Deploy(position);
        
        sut.Facing.ShouldBe(position.Facing);
        
        mech.RotateTorso(HexDirection.Top);
        
        sut.Facing.ShouldBe(HexDirection.Top);
    }
    
    [Theory]
    [InlineData(MountingOptions.Standard, FiringArc.Front)]
    [InlineData(MountingOptions.Rear, FiringArc.Rear)]
    public void GetFiringArcs_ShouldReturnCorrectArcs(MountingOptions mountingOptions, FiringArc expectedArc)
    {
        var sut = new Head("Head",  8, 3);
        
        sut.GetFiringArcs(mountingOptions).ShouldBe([expectedArc]);
    }
    
    [Fact]
    public void GetWeaponsConfigurationOptions_ShouldBeEmpty_WhenNotDeployed()
    {
        var sut = new Head("Head",  8, 3);
        
        sut.GetWeaponsConfigurationOptions().ShouldBeEmpty();
    }
    
    [Fact]
    public void GetWeaponsConfigurationOptions_ShouldIncludeTorsoRotation_WhenDeployed()
    {
        var sut = new Head("Head",  8, 3);
        var mech = new Mech("Test", "TST-1A", 50, [sut], possibleTorsoRotation: 1);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        var options = sut.GetWeaponsConfigurationOptions();

        options.Count.ShouldBe(1);
        options[0].Type.ShouldBe(WeaponConfigurationType.TorsoRotation);
        options[0].AvailableDirections.ShouldBe([HexDirection.TopRight, HexDirection.TopLeft]);
    }
    
    [Fact]
    public void IsWeaponConfigurationApplicable_ShouldReturnTrue_WhenTorsoRotationIsPossible()
    {
        var sut = new Head("Head",  8, 3);
        var mech = new Mech("Test", "TST-1A", 50, [sut], possibleTorsoRotation: 1);
        mech.Deploy(new HexPosition(new HexCoordinates(0, 0), HexDirection.Top));

        sut.IsWeaponConfigurationApplicable(WeaponConfigurationType.TorsoRotation).ShouldBeTrue();
    }
    
    [Fact]
    public void IsWeaponConfigurationApplicable_ShouldReturnFalse_WhenTorsoRotationIsNotPossible()
    {
        var sut = new Head("Head",  8, 3);
        
        sut.IsWeaponConfigurationApplicable(WeaponConfigurationType.TorsoRotation).ShouldBeFalse();
    }
}
