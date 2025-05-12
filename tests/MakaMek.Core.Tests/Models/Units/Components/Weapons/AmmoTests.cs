using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;

public class AmmoTests
{
    [Theory]
    [InlineData(MakaMekComponent.ISAmmoMG, "MachineGun Ammo", MakaMekComponent.ISAmmoMG)]
    [InlineData(MakaMekComponent.ISAmmoAC2, "AC/2 Ammo", MakaMekComponent.ISAmmoAC2)]
    [InlineData(MakaMekComponent.ISAmmoAC5, "AC/5 Ammo", MakaMekComponent.ISAmmoAC5)]
    [InlineData(MakaMekComponent.ISAmmoAC10, "AC/10 Ammo", MakaMekComponent.ISAmmoAC10)]
    [InlineData(MakaMekComponent.ISAmmoAC20, "AC/20 Ammo", MakaMekComponent.ISAmmoAC20)]
    [InlineData(MakaMekComponent.ISAmmoLRM5, "LRM-5 Ammo", MakaMekComponent.ISAmmoLRM5)]
    [InlineData(MakaMekComponent.ISAmmoLRM10, "LRM-10 Ammo", MakaMekComponent.ISAmmoLRM10)]
    [InlineData(MakaMekComponent.ISAmmoLRM15, "LRM-15 Ammo", MakaMekComponent.ISAmmoLRM15)]
    [InlineData(MakaMekComponent.ISAmmoLRM20, "LRM-20 Ammo", MakaMekComponent.ISAmmoLRM20)]
    [InlineData(MakaMekComponent.ISAmmoSRM2, "SRM-2 Ammo", MakaMekComponent.ISAmmoSRM2)]
    [InlineData(MakaMekComponent.ISAmmoSRM4, "SRM-4 Ammo", MakaMekComponent.ISAmmoSRM4)]
    [InlineData(MakaMekComponent.ISAmmoSRM6, "SRM-6 Ammo", MakaMekComponent.ISAmmoSRM6)]
    public void Constructor_InitializesCorrectly(MakaMekComponent ammoComponentType, string expectedName, MakaMekComponent expectedComponentType)
    {
        // Arrange
        var definition = WeaponDefinitions.GetDefinitionByAmmoType(ammoComponentType);
        definition.ShouldNotBeNull();

        // Act
        var sut = new Ammo(definition);

        // Assert
        sut.Name.ShouldBe(expectedName);
        sut.Definition.ShouldBe(definition);
        sut.RemainingShots.ShouldBe(definition.InitialAmmoShots);
        sut.MountedAtSlots.ToList().Count.ShouldBe(0);
        sut.Size.ShouldBe(1);
        sut.IsRemovable.ShouldBeTrue();
        sut.ComponentType.ShouldBe(expectedComponentType);
    }

    [Fact]
    public void Constructor_WithNonAmmoWeapon_ThrowsArgumentException()
    {
        // Arrange
        var definition = WeaponDefinitions.MediumLaser; // Energy weapon without ammo

        // Act & Assert
        Should.Throw<ArgumentException>(() => new Ammo(definition));
    }

    [Fact]
    public void UseShot_DecrementsRemainingShots()
    {
        // Arrange
        var definition = WeaponDefinitions.MachineGun;
        var sut = new Ammo(definition);
        var initialShots = sut.RemainingShots;

        // Act
        var result = sut.UseShot();

        // Assert
        result.ShouldBeTrue();
        sut.RemainingShots.ShouldBe(initialShots - 1);
    }

    [Fact]
    public void UseShot_WhenEmpty_ReturnsFalse()
    {
        // Arrange
        var definition = CreateCustomDefinitionWithShots(0);
        var sut = new Ammo(definition);

        // Act
        var result = sut.UseShot();

        // Assert
        result.ShouldBeFalse();
        sut.RemainingShots.ShouldBe(0);
    }

    [Fact]
    public void Hit_DestroysAmmo()
    {
        // Arrange
        var definition = WeaponDefinitions.MachineGun;
        var sut = new Ammo(definition);

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void CanExplode_ReturnsTrue()
    {
        // Arrange
        var definition = WeaponDefinitions.AC20;
        var sut = new Ammo(definition);

        // Act & Assert
        sut.CanExplode.ShouldBeTrue();
    }

    [Fact]
    public void GetExplosionDamage_ReturnsWeaponDamageTimesRemainingShots()
    {
        // Arrange
        var definition = WeaponDefinitions.AC20;
        var customShots = 10;
        var expectedDamage = definition.TotalDamage * customShots;
        var sut = CreateAmmoWithCustomShots(definition, customShots);

        // Act
        var damage = sut.GetExplosionDamage();

        // Assert
        damage.ShouldBe(expectedDamage);
    }

    [Fact]
    public void GetExplosionDamage_WhenEmpty_ReturnsZero()
    {
        // Arrange
        var definition = WeaponDefinitions.AC20;
        var sut = CreateAmmoWithCustomShots(definition, 0);

        // Act
        var damage = sut.GetExplosionDamage();

        // Assert
        damage.ShouldBe(0);
    }

    [Fact]
    public void Hit_SetsHasExplodedToTrue()
    {
        // Arrange
        var definition = WeaponDefinitions.AC20;
        var sut = new Ammo(definition);

        // Act
        sut.Hit();

        // Assert
        sut.HasExploded.ShouldBeTrue();
    }

    [Fact]
    public void Hit_SetsRemainingToZero()
    {
        // Arrange
        var definition = WeaponDefinitions.AC20;
        var sut = new Ammo(definition);

        // Act
        sut.Hit();

        // Assert
        sut.RemainingShots.ShouldBe(0);
    }

    [Fact]
    public void GetExplosionDamage_AfterHit_ReturnsZero()
    {
        // Arrange
        var definition = WeaponDefinitions.AC20;
        var sut = new Ammo(definition);
        sut.Hit();

        // Act
        var damage = sut.GetExplosionDamage();

        // Assert
        damage.ShouldBe(0);
    }

    // Helper methods for testing
    private static Ammo CreateAmmoWithCustomShots(WeaponDefinition baseDefinition, int shots)
    {
        var definition = CreateCustomDefinitionWithShots(shots, baseDefinition);
        return new Ammo(definition);
    }

    private static WeaponDefinition CreateCustomDefinitionWithShots(int shots, WeaponDefinition? baseDefinition = null)
    {
        baseDefinition ??= WeaponDefinitions.AC20;
        
        return new WeaponDefinition(
            name: baseDefinition.Name,
            elementaryDamage: baseDefinition.ElementaryDamage,
            heat: baseDefinition.Heat,
            minimumRange: baseDefinition.MinimumRange,
            shortRange: baseDefinition.ShortRange,
            mediumRange: baseDefinition.MediumRange,
            longRange: baseDefinition.LongRange,
            type: baseDefinition.Type,
            battleValue: baseDefinition.BattleValue,
            clusters: baseDefinition.Clusters,
            clusterSize: baseDefinition.ClusterSize,
            weaponComponentType: baseDefinition.WeaponComponentType,
            ammoComponentType: baseDefinition.AmmoComponentType,
            initialAmmoShots: shots);
    }
}
