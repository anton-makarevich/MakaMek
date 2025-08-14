using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics;

public class WeaponSelectionCalculatorTests
{
    private readonly WeaponSelectionCalculator _sut;
    private readonly Mech _proneMech;
    private readonly Mech _standingMech;
    private readonly Weapon _leftArmWeapon;
    private readonly Weapon _rightArmWeapon;
    private readonly Weapon _torsoWeapon;
    private readonly Weapon _legWeapon;
    private readonly Weapon _unavailableWeapon;

    public WeaponSelectionCalculatorTests()
    {
        _sut = new WeaponSelectionCalculator();
        
        // Create mock mechs
        _proneMech = Substitute.For<Mech>();
        _proneMech.IsProne.Returns(true);
        _proneMech.WeaponAttackState.Returns(new UnitWeaponAttackState());
        
        _standingMech = Substitute.For<Mech>();
        _standingMech.IsProne.Returns(false);
        _standingMech.WeaponAttackState.Returns(new UnitWeaponAttackState());
        
        // Create mock weapons with different locations
        _leftArmWeapon = CreateMockWeapon(PartLocation.LeftArm, true);
        _rightArmWeapon = CreateMockWeapon(PartLocation.RightArm, true);
        _torsoWeapon = CreateMockWeapon(PartLocation.CenterTorso, true);
        _legWeapon = CreateMockWeapon(PartLocation.LeftLeg, true);
        _unavailableWeapon = CreateMockWeapon(PartLocation.LeftArm, false);
    }

    private static Weapon CreateMockWeapon(PartLocation location, bool isAvailable)
    {
        var weapon = Substitute.For<Weapon>();
        weapon.IsAvailable.Returns(isAvailable);
        
        var part = Substitute.For<UnitPart>();
        part.Location.Returns(location);
        weapon.MountedOn.Returns(part);
        
        return weapon;
    }

    [Fact]
    public void IsWeaponAvailable_WithUnavailableWeapon_ShouldReturnFalse()
    {
        // Act
        var result = _sut.IsWeaponAvailable(_unavailableWeapon, _standingMech);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsWeaponAvailable_WithStandingMech_ShouldReturnTrue()
    {
        // Act & Assert
        _sut.IsWeaponAvailable(_leftArmWeapon, _standingMech).ShouldBeTrue();
        _sut.IsWeaponAvailable(_rightArmWeapon, _standingMech).ShouldBeTrue();
        _sut.IsWeaponAvailable(_torsoWeapon, _standingMech).ShouldBeTrue();
        _sut.IsWeaponAvailable(_legWeapon, _standingMech).ShouldBeTrue();
    }

    [Fact]
    public void IsWeaponAvailable_WithProneMechAndLegWeapon_ShouldReturnFalse()
    {
        // Act
        var result = _sut.IsWeaponAvailable(_legWeapon, _proneMech);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsWeaponAvailable_WithProneMechAndTorsoWeapon_ShouldReturnTrue()
    {
        // Act
        var result = _sut.IsWeaponAvailable(_torsoWeapon, _proneMech);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsWeaponAvailable_WithProneMechAndNoCommittedArm_ShouldReturnTrueForAnyArm()
    {
        // Act & Assert
        _sut.IsWeaponAvailable(_leftArmWeapon, _proneMech).ShouldBeTrue();
        _sut.IsWeaponAvailable(_rightArmWeapon, _proneMech).ShouldBeTrue();
    }

    [Fact]
    public void IsWeaponAvailable_WithProneMechAndCommittedLeftArm_ShouldOnlyAllowLeftArmWeapons()
    {
        // Arrange
        _proneMech.WeaponAttackState.SetWeaponTarget(_leftArmWeapon, Substitute.For<Unit>(), _proneMech);

        // Act & Assert
        _sut.IsWeaponAvailable(_leftArmWeapon, _proneMech).ShouldBeTrue();
        _sut.IsWeaponAvailable(_rightArmWeapon, _proneMech).ShouldBeFalse();
        _sut.IsWeaponAvailable(_torsoWeapon, _proneMech).ShouldBeTrue(); // Torso weapons always available
    }

    [Fact]
    public void GetWeaponRestrictionReason_WithUnavailableWeapon_ShouldReturnNotAvailable()
    {
        // Act
        var result = _sut.GetWeaponRestrictionReason(_unavailableWeapon, _standingMech);

        // Assert
        result.ShouldBe("WeaponRestriction_NotAvailable");
    }

    [Fact]
    public void GetWeaponRestrictionReason_WithProneMechAndLegWeapon_ShouldReturnProneLegs()
    {
        // Act
        var result = _sut.GetWeaponRestrictionReason(_legWeapon, _proneMech);

        // Assert
        result.ShouldBe("WeaponRestriction_ProneLegs");
    }

    [Fact]
    public void GetWeaponRestrictionReason_WithProneMechAndCommittedArmConflict_ShouldReturnProneOtherArm()
    {
        // Arrange
        _proneMech.WeaponAttackState.SetWeaponTarget(_leftArmWeapon, Substitute.For<Unit>(), _proneMech);

        // Act
        var result = _sut.GetWeaponRestrictionReason(_rightArmWeapon, _proneMech);

        // Assert
        result.ShouldBe("WeaponRestriction_ProneOtherArm");
    }

    [Fact]
    public void GetWeaponRestrictionReason_WithAvailableWeapon_ShouldReturnNull()
    {
        // Act
        var result = _sut.GetWeaponRestrictionReason(_leftArmWeapon, _standingMech);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void IsWeaponAvailableWhenProne_WithNullLocation_ShouldReturnFalse()
    {
        // Arrange
        var weaponWithoutLocation = Substitute.For<Weapon>();
        weaponWithoutLocation.MountedOn.Returns((UnitPart?)null);

        // Act
        var result = _sut.IsWeaponAvailableWhenProne(weaponWithoutLocation, _proneMech);

        // Assert
        result.ShouldBeFalse();
    }
}
