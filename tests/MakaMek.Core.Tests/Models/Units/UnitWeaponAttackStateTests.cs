using NSubstitute;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units;

public class UnitWeaponAttackStateTests
{
    private readonly UnitWeaponAttackState _sut;
    private readonly Unit _attacker;
    private readonly Unit _target1;
    private readonly Unit _target2;
    private readonly Weapon _leftArmWeapon;
    private readonly Weapon _rightArmWeapon;
    private readonly Weapon _torsoWeapon;
    private readonly Weapon _legWeapon;

    public UnitWeaponAttackStateTests()
    {
        _sut = new UnitWeaponAttackState();
        
        // Create mock units
        _attacker = Substitute.For<Mech>();
        _target1 = Substitute.For<Unit>();
        _target2 = Substitute.For<Unit>();
        
        // Create mock weapons with different locations
        _leftArmWeapon = Substitute.For<Weapon>();
        _leftArmWeapon.MountedOn.Returns(CreateMockPart(PartLocation.LeftArm));
        
        _rightArmWeapon = Substitute.For<Weapon>();
        _rightArmWeapon.MountedOn.Returns(CreateMockPart(PartLocation.RightArm));
        
        _torsoWeapon = Substitute.For<Weapon>();
        _torsoWeapon.MountedOn.Returns(CreateMockPart(PartLocation.CenterTorso));
        
        _legWeapon = Substitute.For<Weapon>();
        _legWeapon.MountedOn.Returns(CreateMockPart(PartLocation.LeftLeg));
        
        // Setup attacker position for primary target calculation
        _attacker.Position.Returns(new HexPosition(new HexCoordinates(1, 1), HexDirection.Bottom));
        _target1.Position.Returns(new HexPosition(new HexCoordinates(2, 1), HexDirection.Bottom));
        _target2.Position.Returns(new HexPosition(new HexCoordinates(1, 2), HexDirection.Bottom));
    }

    private static UnitPart CreateMockPart(PartLocation location)
    {
        var part = Substitute.For<UnitPart>();
        part.Location.Returns(location);
        return part;
    }

    [Fact]
    public void SetWeaponTarget_ShouldAddWeaponToTargets()
    {
        // Act
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Assert
        _sut.WeaponTargets.ShouldContainKeyAndValue(_leftArmWeapon, _target1);
        _sut.SelectedWeapons.ShouldContain(_leftArmWeapon);
        _sut.AllTargets.ShouldContain(_target1);
    }

    [Fact]
    public void SetWeaponTarget_WithProneMech_ShouldSetCommittedArm()
    {
        // Arrange
        ((Mech)_attacker).IsProne.Returns(true);

        // Act
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Assert
        _sut.CommittedArmLocation.ShouldBe(PartLocation.LeftArm);
    }

    [Fact]
    public void SetWeaponTarget_WithNonProneMech_ShouldNotSetCommittedArm()
    {
        // Arrange
        ((Mech)_attacker).IsProne.Returns(false);

        // Act
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Assert
        _sut.CommittedArmLocation.ShouldBeNull();
    }

    [Fact]
    public void SetWeaponTarget_WithSingleTarget_ShouldSetPrimaryTarget()
    {
        // Act
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Assert
        _sut.PrimaryTarget.ShouldBe(_target1);
    }

    [Fact]
    public void RemoveWeaponTarget_ShouldRemoveWeaponFromTargets()
    {
        // Arrange
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Act
        _sut.RemoveWeaponTarget(_leftArmWeapon, _attacker);

        // Assert
        _sut.WeaponTargets.ShouldNotContainKey(_leftArmWeapon);
        _sut.SelectedWeapons.ShouldNotContain(_leftArmWeapon);
    }

    [Fact]
    public void RemoveWeaponTarget_WithProneMech_ShouldClearCommittedArmWhenNoArmWeaponsLeft()
    {
        // Arrange
        ((Mech)_attacker).IsProne.Returns(true);
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Act
        _sut.RemoveWeaponTarget(_leftArmWeapon, _attacker);

        // Assert
        _sut.CommittedArmLocation.ShouldBeNull();
    }

    [Fact]
    public void ClearAllWeaponTargets_ShouldClearAllState()
    {
        // Arrange
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);
        _sut.SetWeaponTarget(_torsoWeapon, _target2, _attacker);

        // Act
        _sut.ClearAllWeaponTargets();

        // Assert
        _sut.WeaponTargets.ShouldBeEmpty();
        _sut.SelectedWeapons.ShouldBeEmpty();
        _sut.AllTargets.ShouldBeEmpty();
        _sut.PrimaryTarget.ShouldBeNull();
        _sut.CommittedArmLocation.ShouldBeNull();
    }

    [Fact]
    public void IsWeaponAssigned_WithAssignedWeapon_ShouldReturnTrue()
    {
        // Arrange
        _sut.SetWeaponTarget(_leftArmWeapon, _target1, _attacker);

        // Act & Assert
        _sut.IsWeaponAssigned(_leftArmWeapon).ShouldBeTrue();
        _sut.IsWeaponAssigned(_leftArmWeapon, _target1).ShouldBeTrue();
        _sut.IsWeaponAssigned(_leftArmWeapon, _target2).ShouldBeFalse();
    }

    [Fact]
    public void IsWeaponAssigned_WithUnassignedWeapon_ShouldReturnFalse()
    {
        // Act & Assert
        _sut.IsWeaponAssigned(_leftArmWeapon).ShouldBeFalse();
        _sut.IsWeaponAssigned(_leftArmWeapon, _target1).ShouldBeFalse();
    }
}
