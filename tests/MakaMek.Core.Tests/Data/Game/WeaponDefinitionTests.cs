using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game;

public class WeaponDefinitionTests
{
    [Fact]
    public void Constructor_InitializesPropertiesCorrectly()
    {
        // Arrange & Act
        var definition = new WeaponDefinition(
            name: "Test Weapon",
            elementaryDamage: 5,
            heat: 3,
            minimumRange: 2,
            shortRange: 4,
            mediumRange: 8,
            longRange: 12,
            type: WeaponType.Energy,
            battleValue: 50,
            clusters: 2,
            clusterSize: 3,
            weaponComponentType: MakaMekComponent.MediumLaser,
            ammoComponentType: MakaMekComponent.ISAmmoLRM5,
            initialAmmoShots: 24);

        // Assert
        definition.Name.ShouldBe("Test Weapon");
        definition.ElementaryDamage.ShouldBe(5);
        definition.Heat.ShouldBe(3);
        definition.MinimumRange.ShouldBe(2);
        definition.ShortRange.ShouldBe(4);
        definition.MediumRange.ShouldBe(8);
        definition.LongRange.ShouldBe(12);
        definition.Type.ShouldBe(WeaponType.Energy);
        definition.BattleValue.ShouldBe(50);
        definition.Clusters.ShouldBe(2);
        definition.ClusterSize.ShouldBe(3);
        definition.WeaponComponentType.ShouldBe(MakaMekComponent.MediumLaser);
        definition.AmmoComponentType.ShouldBe(MakaMekComponent.ISAmmoLRM5);
        definition.InitialAmmoShots.ShouldBe(24);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(MakaMekComponent.ISAmmoLRM5, true)]
    public void RequiresAmmo_ReturnsCorrectValue(MakaMekComponent? ammoComponentType, bool expected)
    {
        // Arrange
        var definition = new WeaponDefinition(
            name: "Test Weapon",
            elementaryDamage: 5,
            heat: 3,
            minimumRange: 0,
            shortRange: 3,
            mediumRange: 6,
            longRange: 9,
            type: WeaponType.Energy,
            battleValue: 50,
            ammoComponentType: ammoComponentType);

        // Act & Assert
        definition.RequiresAmmo.ShouldBe(expected);
    }

    [Theory]
    [InlineData(5, 1, 1, 5)]  // Single shot weapon
    [InlineData(2, 2, 3, 12)] // Clustered weapon (e.g., LRM)
    [InlineData(0, 1, 1, 0)]  // Zero damage weapon
    public void TotalDamage_CalculatedCorrectly(int elementaryDamage, int clusters, int clusterSize, int expected)
    {
        // Arrange
        var definition = new WeaponDefinition(
            name: "Test Weapon",
            elementaryDamage: elementaryDamage,
            heat: 3,
            minimumRange: 0,
            shortRange: 3,
            mediumRange: 6,
            longRange: 9,
            type: WeaponType.Energy,
            battleValue: 50,
            clusters: clusters,
            clusterSize: clusterSize);

        // Act & Assert
        definition.TotalDamage.ShouldBe(expected);
    }

    [Theory]
    [InlineData(1, 1, 1)]  // Single shot weapon
    [InlineData(2, 3, 6)]  // Clustered weapon (e.g., LRM)
    public void WeaponSize_CalculatedCorrectly(int clusters, int clusterSize, int expected)
    {
        // Arrange
        var definition = new WeaponDefinition(
            name: "Test Weapon",
            elementaryDamage: 5,
            heat: 3,
            minimumRange: 0,
            shortRange: 3,
            mediumRange: 6,
            longRange: 9,
            type: WeaponType.Energy,
            battleValue: 50,
            clusters: clusters,
            clusterSize: clusterSize);

        // Act & Assert
        definition.WeaponSize.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, WeaponRange.OutOfRange)]  // Zero or negative distance
    [InlineData(-1, WeaponRange.OutOfRange)] // Negative distance
    [InlineData(2, WeaponRange.Minimum)]     // Within minimum range
    [InlineData(3, WeaponRange.Minimum)]     // At minimum range boundary
    [InlineData(4, WeaponRange.Short)]       // At short range boundary
    [InlineData(6, WeaponRange.Short)]       // Within short range
    [InlineData(8, WeaponRange.Medium)]      // At medium range boundary
    [InlineData(10, WeaponRange.Medium)]     // Within medium range
    [InlineData(12, WeaponRange.Long)]       // At long range boundary
    [InlineData(13, WeaponRange.OutOfRange)] // Beyond long range
    public void GetRangeBracket_ReturnsCorrectRange(int distance, WeaponRange expected)
    {
        // Arrange
        var definition = new WeaponDefinition(
            name: "Test Weapon",
            elementaryDamage: 5,
            heat: 3,
            minimumRange: 3,
            shortRange: 4,
            mediumRange: 8,
            longRange: 12,
            type: WeaponType.Energy,
            battleValue: 50);

        // Act
        var result = definition.GetRangeBracket(distance);

        // Assert
        result.ShouldBe(expected);
    }
}
