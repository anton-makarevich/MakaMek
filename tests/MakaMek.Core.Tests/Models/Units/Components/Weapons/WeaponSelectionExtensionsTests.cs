using NSubstitute;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Services.Localization;
using Sanet.MakaMek.Core.Tests.Data.Community;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Core.Utils.TechRules;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;

public class WeaponSelectionExtensionsTests
{
    private readonly Mech _mech;
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    public WeaponSelectionExtensionsTests()
    {
        var mechData = MechFactoryTests.CreateDummyMechData();
        _mech = new MechFactory(new ClassicBattletechRulesProvider(), _localizationService).Create(mechData);
    }

    private Weapon CreateWeapon(PartLocation location, bool isAvailable)
    {
        var part = _mech.Parts.First(p => p.Location == location);
        var weapon = new MediumLaser();
        part.TryAddComponent(weapon);
        
        if (!isAvailable)
            weapon.Hit();
        
        return weapon;
    }
    
    [Fact]
    public void IsAvailableForAttack_ShouldReturnFalse_WithNoMountedOn()
    {
        // Arrange
        var sut = new MediumLaser();
        
        // Act & Assert
        sut.IsAvailableForAttack().ShouldBeFalse();
    }

    [Fact]
    public void IsAvailableForAttack_ShouldReturnFalse_WithUnavailableWeapon()
    {
        // Arrange
        var sut = CreateWeapon(PartLocation.LeftArm, false);
        
        // Act
        var result = sut.IsAvailableForAttack();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsAvailableForAttack_ShouldReturnTrue_WithStandingMech()
    {
        // Arrange
        var sut = CreateWeapon(PartLocation.LeftArm, true);
        
        // Act & Assert
        sut.IsAvailableForAttack().ShouldBeTrue();
    }

    [Theory]
    [InlineData(PartLocation.LeftLeg)]
    [InlineData(PartLocation.RightLeg)]
    public void IsAvailableForAttack_ShouldReturnFalse_WithProneMechAndLegWeapon(PartLocation partLocation)
    {
        var sut = CreateWeapon(partLocation, true);
        _mech.SetProne();
        
        // Act
        var result = sut.IsAvailableForAttack();

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(PartLocation.CenterTorso)]
    [InlineData(PartLocation.LeftTorso)]
    [InlineData(PartLocation.RightTorso)]
    public void IsAvailableForAttack_ShouldReturnTrue_WithProneMechAndTorsoWeapon(PartLocation partLocation)
    {
        // Arrange
        var sut = CreateWeapon(partLocation, true);
        _mech.SetProne();
        
        // Act
        var result = sut.IsAvailableForAttack();

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(PartLocation.LeftArm)]
    [InlineData(PartLocation.RightArm)]
    public void IsAvailableForAttack_ShouldReturnTrueForAnyArm_WithProneMechAndNoCommittedArm(PartLocation arm)
    {
        // Arrange
        var sut = CreateWeapon(arm, true);
        _mech.SetProne();
        
        // Act & Assert
        sut.IsAvailableForAttack().ShouldBeTrue();
    }

    [Fact]
    public void IsAvailableForAttack_ShouldAllowLeftArmWeapons_WithProneMechAndCommittedLeftArm()
    {
        // Arrange
        var sut = CreateWeapon(PartLocation.LeftArm, true);
        _mech.WeaponAttackState.SetWeaponTarget(sut, _mech, _mech);

        // Act & Assert
        sut.IsAvailableForAttack().ShouldBeTrue();
    }
    
    [Fact]
    public void IsAvailableForAttack_ShouldNotAllowRightArmWeapons_WithProneMechAndCommittedLeftArm()
    {
        // Arrange
        var leftArmWeapon = CreateWeapon(PartLocation.LeftArm, true);
        var sut = CreateWeapon(PartLocation.RightArm, true);
        _mech.SetProne();
        _mech.WeaponAttackState.SetWeaponTarget(leftArmWeapon, _mech, _mech);

        // Act & Assert
        sut.IsAvailableForAttack().ShouldBeFalse();
    }

    [Fact]
    public void GetWeaponRestrictionReason_WithUnavailableWeapon_ShouldReturnNotAvailable()
    {
        // Arrange
        var sut = CreateWeapon(PartLocation.LeftArm, false);
        _localizationService.GetString("WeaponRestriction_NotAvailable").Returns("NotAvailable");
        
        // Act
        var result = sut.GetWeaponRestrictionReason(_localizationService);

        // Assert
        result.ShouldBe("NotAvailable");
    }

    [Fact]
    public void GetWeaponRestrictionReason_WithProneMechAndLegWeapon_ShouldReturnProneLegs()
    {
        // Arrange
        var sut = CreateWeapon(PartLocation.LeftLeg, true);
        _mech.SetProne();
        _localizationService.GetString("WeaponRestriction_ProneLegs").Returns("ProneLegs");
        
        // Act
        var result = sut.GetWeaponRestrictionReason(_localizationService);

        // Assert
        result.ShouldBe("ProneLegs");
    }

    [Fact]
    public void GetWeaponRestrictionReason_WithProneMechAndCommittedArmConflict_ShouldReturnProneOtherArm()
    {
        // Arrange
        var leftArmWeapon = CreateWeapon(PartLocation.LeftArm, true);
        var sut = CreateWeapon(PartLocation.RightArm, true);
        _mech.SetProne();
        _mech.WeaponAttackState.SetWeaponTarget(leftArmWeapon, _mech, _mech);
        _localizationService.GetString("WeaponRestriction_ProneOtherArm").Returns("ProneOtherArm");
        
        // Act
        var result = sut.GetWeaponRestrictionReason(_localizationService);

        // Assert
        result.ShouldBe("ProneOtherArm");
    }

    [Fact]
    public void GetWeaponRestrictionReason_WithAvailableWeapon_ShouldReturnEmpty()
    {
        // Arrange
        var sut = CreateWeapon(PartLocation.LeftArm, true);
        // Act
        var result = sut.GetWeaponRestrictionReason(_localizationService);

        // Assert
        result.ShouldBe(string.Empty);
    }
}
