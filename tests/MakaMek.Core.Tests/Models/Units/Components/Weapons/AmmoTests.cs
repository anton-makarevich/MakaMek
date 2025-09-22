using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;

public class AmmoTests
{
    [Theory]
    [InlineData("MachineGun", MakaMekComponent.ISAmmoMG, 200, 1)]
    [InlineData("AC2", MakaMekComponent.ISAmmoAC2, 200, 2)]
    [InlineData("AC5", MakaMekComponent.ISAmmoAC5, 200, 5)]
    [InlineData("AC10", MakaMekComponent.ISAmmoAC10, 200, 10)]
    [InlineData("AC20", MakaMekComponent.ISAmmoAC20, 200, 20)]
    [InlineData("LRM-5", MakaMekComponent.ISAmmoLRM5, 24, 5)]
    [InlineData("LRM-10", MakaMekComponent.ISAmmoLRM10, 24, 10)]
    [InlineData("LRM-15", MakaMekComponent.ISAmmoLRM15, 24, 15)]
    [InlineData("LRM-20", MakaMekComponent.ISAmmoLRM20, 24, 20)]
    [InlineData("SRM-2", MakaMekComponent.ISAmmoSRM2, 50, 2)]
    [InlineData("SRM-4", MakaMekComponent.ISAmmoSRM4, 50, 4)]
    [InlineData("SRM-6", MakaMekComponent.ISAmmoSRM6, 50, 6)]
    public void Constructor_InitializesCorrectly(string weaponName, MakaMekComponent ammoComponentType, int initialShots, int expectedDamage)
    {
        // Arrange
        var definition = CreateTestWeaponDefinition(weaponName, ammoComponentType, expectedDamage);
        
        // Act
        var sut = new Ammo(definition, initialShots);

        // Assert
        sut.Name.ShouldBe($"{weaponName} Ammo");
        sut.RemainingShots.ShouldBe(initialShots);
        sut.MountedAtSlots.ToList().Count.ShouldBe(0);
        sut.Size.ShouldBe(1);
        sut.IsRemovable.ShouldBeTrue();
        sut.ComponentType.ShouldBe(ammoComponentType);
        sut.Definition.ShouldBe(definition);
    }

    [Fact]
    public void Constructor_ThrowsException_WhenWeaponDoesNotRequireAmmo()
    {
        // Arrange
        var definition = new WeaponDefinition(
            Name: "Medium Laser",
            ElementaryDamage: 5,
            Heat: 3,
            MinimumRange: 0,
            ShortRange: 3,
            MediumRange: 6,
            LongRange: 9,
            Type: WeaponType.Energy,
            BattleValue: 46,
            WeaponComponentType: MakaMekComponent.MediumLaser);

        // Act & Assert
        Should.Throw<ArgumentException>(() => new Ammo(definition, 200))
            .Message.ShouldBe($"Cannot create ammo for weapon that doesn't require it: Medium Laser");
        
    }

    [Fact]
    public void UseShot_DecrementsRemainingShots()
    {
        // Arrange
        var definition = CreateTestWeaponDefinition("AC20", MakaMekComponent.ISAmmoAC20, 20);
        var sut = new Ammo(definition, 200);

        // Act
        sut.UseShot();

        // Assert
        sut.RemainingShots.ShouldBe(199);
    }

    [Fact]
    public void UseShot_WhenEmpty_DoesNotDecrementBelowZero()
    {
        // Arrange
        var definition = CreateTestWeaponDefinition("AC20", MakaMekComponent.ISAmmoAC20, 20);
        var sut = new Ammo(definition, 0);

        // Act
        var result = sut.UseShot();

        // Assert
        sut.RemainingShots.ShouldBe(0);
        result.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysAmmo()
    {
        // Arrange
        var definition = CreateTestWeaponDefinition("AC20", MakaMekComponent.ISAmmoAC20, 20);
        var sut = new Ammo(definition, 200);

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void CanExplode_ReturnsTrue()
    {
        // Arrange
        var definition = CreateTestWeaponDefinition("AC20", MakaMekComponent.ISAmmoAC20, 20);
        var sut = new Ammo(definition, 10);

        // Act & Assert
        sut.CanExplode.ShouldBeTrue();
    }

    [Fact]
    public void GetExplosionDamage_ReturnsTotalDamageTimesRemainingShots()
    {
        // Arrange
        var definition = CreateTestWeaponDefinition("AC20", MakaMekComponent.ISAmmoAC20, 20);
        var sut = new Ammo(definition, 10);

        // Act
        var damage = sut.GetExplosionDamage();

        // Assert
        damage.ShouldBe(200); // 20 damage per shot * 10 remaining shots
    }

    [Fact]
    public void GetExplosionDamage_WhenEmpty_ReturnsZero()
    {
        // Arrange
        var definition = CreateTestWeaponDefinition("AC20", MakaMekComponent.ISAmmoAC20, 20);
        var sut = new Ammo(definition, 0);

        // Act
        var damage = sut.GetExplosionDamage();

        // Assert
        damage.ShouldBe(0);
    }

    [Fact]
    public void Hit_SetsHasExplodedToTrue()
    {
        // Arrange
        var definition = CreateTestWeaponDefinition("AC20", MakaMekComponent.ISAmmoAC20, 20);
        var sut = new Ammo(definition, 10);

        // Act
        sut.Hit();

        // Assert
        sut.HasExploded.ShouldBeTrue();
    }

    [Fact]
    public void Hit_SetsRemainingToZero()
    {
        // Arrange
        var definition = CreateTestWeaponDefinition("AC20", MakaMekComponent.ISAmmoAC20, 20);
        var sut = new Ammo(definition, 10);

        // Act
        sut.Hit();

        // Assert
        sut.RemainingShots.ShouldBe(0);
    }

    [Fact]
    public void GetExplosionDamage_AfterHit_ReturnsZero()
    {
        // Arrange
        var definition = CreateTestWeaponDefinition("AC20", MakaMekComponent.ISAmmoAC20, 20);
        var sut = new Ammo(definition, 10);
        sut.Hit();

        // Act
        var damage = sut.GetExplosionDamage();

        // Assert
        damage.ShouldBe(0);
    }
    
    private static WeaponDefinition CreateTestWeaponDefinition(string name, MakaMekComponent ammoComponentType, int damage)
    {
        return new WeaponDefinition(
            Name: name,
            ElementaryDamage: damage,
            Heat: 0,
            MinimumRange: 0,
            ShortRange: 1,
            MediumRange: 2,
            LongRange: 3,
            Type: WeaponType.Ballistic,
            BattleValue: 1,
            WeaponComponentType: MakaMekComponent.MachineGun,
            AmmoComponentType: ammoComponentType);
    }
}
