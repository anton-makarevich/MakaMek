using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game;

public class WeaponDefinitionsTests
{
    [Theory]
    [InlineData(MakaMekComponent.MediumLaser, "Medium Laser", 5, 3, 0, 3, 6, 9, WeaponType.Energy, 46, 1, 1, false)]
    [InlineData(MakaMekComponent.LargeLaser, "Large Laser", 8, 8, 0, 5, 10, 15, WeaponType.Energy, 100, 1, 1, false)]
    [InlineData(MakaMekComponent.SmallLaser, "Small Laser", 3, 1, 0, 1, 2, 3, WeaponType.Energy, 9, 1, 1, false)]
    [InlineData(MakaMekComponent.PPC, "PPC", 10, 10, 3, 6, 12, 18, WeaponType.Energy, 175, 1, 1, false)]
    [InlineData(MakaMekComponent.MachineGun, "Machine Gun", 2, 0, 0, 1, 2, 3, WeaponType.Ballistic, 5, 1, 1, true)]
    [InlineData(MakaMekComponent.AC2, "AC/2", 2, 1, 4, 8, 16, 24, WeaponType.Ballistic, 37, 1, 1, true)]
    [InlineData(MakaMekComponent.AC5, "AC/5", 5, 1, 3, 6, 12, 18, WeaponType.Ballistic, 70, 1, 1, true)]
    [InlineData(MakaMekComponent.AC10, "AC/10", 10, 3, 0, 5, 10, 15, WeaponType.Ballistic, 110, 1, 1, true)]
    [InlineData(MakaMekComponent.AC20, "AC/20", 20, 7, 0, 3, 6, 9, WeaponType.Ballistic, 178, 1, 1, true)]
    [InlineData(MakaMekComponent.LRM5, "LRM-5", 1, 2, 6, 7, 14, 21, WeaponType.Missile, 45, 1, 5, true)]
    [InlineData(MakaMekComponent.LRM10, "LRM-10", 1, 4, 6, 7, 14, 21, WeaponType.Missile, 90, 2, 5, true)]
    [InlineData(MakaMekComponent.LRM15, "LRM-15", 1, 5, 6, 7, 14, 21, WeaponType.Missile, 135, 3, 5, true)]
    [InlineData(MakaMekComponent.LRM20, "LRM-20", 1, 6, 6, 7, 14, 21, WeaponType.Missile, 180, 4, 5, true)]
    [InlineData(MakaMekComponent.SRM2, "SRM-2", 2, 2, 0, 3, 6, 9, WeaponType.Missile, 15, 1, 2, true)]
    [InlineData(MakaMekComponent.SRM4, "SRM-4", 2, 3, 0, 3, 6, 9, WeaponType.Missile, 30, 1, 4, true)]
    [InlineData(MakaMekComponent.SRM6, "SRM-6", 2, 4, 0, 3, 6, 9, WeaponType.Missile, 45, 1, 6, true)]
    public void WeaponDefinition_HasCorrectValues(
        MakaMekComponent componentType, 
        string name, 
        int elementaryDamage, 
        int heat, 
        int minimumRange, 
        int shortRange, 
        int mediumRange, 
        int longRange, 
        WeaponType type, 
        int battleValue, 
        int clusters, 
        int clusterSize, 
        bool requiresAmmo)
    {
        // Arrange
        var definition = WeaponDefinitions.GetDefinitionByWeaponType(componentType);
        
        // Assert
        definition.ShouldNotBeNull();
        definition.Name.ShouldBe(name);
        definition.ElementaryDamage.ShouldBe(elementaryDamage);
        definition.Heat.ShouldBe(heat);
        definition.MinimumRange.ShouldBe(minimumRange);
        definition.ShortRange.ShouldBe(shortRange);
        definition.MediumRange.ShouldBe(mediumRange);
        definition.LongRange.ShouldBe(longRange);
        definition.Type.ShouldBe(type);
        definition.BattleValue.ShouldBe(battleValue);
        definition.Clusters.ShouldBe(clusters);
        definition.ClusterSize.ShouldBe(clusterSize);
        definition.RequiresAmmo.ShouldBe(requiresAmmo);
    }
    
    [Theory]
    [InlineData(MakaMekComponent.ISAmmoMG, MakaMekComponent.MachineGun, 200)]
    [InlineData(MakaMekComponent.ISAmmoAC2, MakaMekComponent.AC2, 45)]
    [InlineData(MakaMekComponent.ISAmmoAC5, MakaMekComponent.AC5, 20)]
    [InlineData(MakaMekComponent.ISAmmoAC10, MakaMekComponent.AC10, 10)]
    [InlineData(MakaMekComponent.ISAmmoAC20, MakaMekComponent.AC20, 5)]
    [InlineData(MakaMekComponent.ISAmmoLRM5, MakaMekComponent.LRM5, 24)]
    [InlineData(MakaMekComponent.ISAmmoLRM10, MakaMekComponent.LRM10, 12)]
    [InlineData(MakaMekComponent.ISAmmoLRM15, MakaMekComponent.LRM15, 8)]
    [InlineData(MakaMekComponent.ISAmmoLRM20, MakaMekComponent.LRM20, 6)]
    [InlineData(MakaMekComponent.ISAmmoSRM2, MakaMekComponent.SRM2, 50)]
    [InlineData(MakaMekComponent.ISAmmoSRM4, MakaMekComponent.SRM4, 25)]
    [InlineData(MakaMekComponent.ISAmmoSRM6, MakaMekComponent.SRM6, 15)]
    public void AmmoComponentType_HasCorrectInitialShots(
        MakaMekComponent ammoComponentType, 
        MakaMekComponent weaponComponentType, 
        int expectedInitialShots)
    {
        // Arrange
        var definition = WeaponDefinitions.GetDefinitionByAmmoType(ammoComponentType);
        var weaponDefinition = WeaponDefinitions.GetDefinitionByWeaponType(weaponComponentType);
        
        // Assert
        definition.ShouldNotBeNull();
        definition.InitialAmmoShots.ShouldBe(expectedInitialShots);
        definition.ShouldBe(weaponDefinition); // Same definition should be returned by both lookup methods
    }
    
    [Fact]
    public void GetDefinitionByWeaponType_WithInvalidType_ReturnsNull()
    {
        // Act
        var result = WeaponDefinitions.GetDefinitionByWeaponType(MakaMekComponent.ISAmmoMG);
        
        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void GetDefinitionByAmmoType_WithInvalidType_ReturnsNull()
    {
        // Act
        var result = WeaponDefinitions.GetDefinitionByAmmoType(MakaMekComponent.MediumLaser);
        
        // Assert
        result.ShouldBeNull();
    }
    
    [Theory]
    [InlineData(MakaMekComponent.MachineGun, 2)]
    [InlineData(MakaMekComponent.AC2, 2)]
    [InlineData(MakaMekComponent.AC5, 5)]
    [InlineData(MakaMekComponent.AC10, 10)]
    [InlineData(MakaMekComponent.AC20, 20)]
    [InlineData(MakaMekComponent.LRM5, 5)]
    [InlineData(MakaMekComponent.LRM10, 10)]
    [InlineData(MakaMekComponent.LRM15, 15)]
    [InlineData(MakaMekComponent.LRM20, 20)]
    [InlineData(MakaMekComponent.SRM2, 4)]
    [InlineData(MakaMekComponent.SRM4, 8)]
    [InlineData(MakaMekComponent.SRM6, 12)]
    public void TotalDamage_CalculatedCorrectly(MakaMekComponent componentType, int expectedDamage)
    {
        // Arrange
        var definition = WeaponDefinitions.GetDefinitionByWeaponType(componentType);
        
        // Assert
        definition.ShouldNotBeNull();
        definition.TotalDamage.ShouldBe(expectedDamage);
    }
    
    [Theory]
    [InlineData(MakaMekComponent.LRM5, 5)]
    [InlineData(MakaMekComponent.LRM10, 10)]
    [InlineData(MakaMekComponent.LRM15, 15)]
    [InlineData(MakaMekComponent.LRM20, 20)]
    [InlineData(MakaMekComponent.SRM2, 2)]
    [InlineData(MakaMekComponent.SRM4, 4)]
    [InlineData(MakaMekComponent.SRM6, 6)]
    public void WeaponSize_CalculatedCorrectly(MakaMekComponent componentType, int expectedSize)
    {
        // Arrange
        var definition = WeaponDefinitions.GetDefinitionByWeaponType(componentType);
        
        // Assert
        definition.ShouldNotBeNull();
        definition.WeaponSize.ShouldBe(expectedSize);
    }
    
    [Theory]
    [InlineData(MakaMekComponent.MediumLaser, 0, WeaponRange.OutOfRange)]
    [InlineData(MakaMekComponent.MediumLaser, 1, WeaponRange.Short)]
    [InlineData(MakaMekComponent.MediumLaser, 3, WeaponRange.Short)]
    [InlineData(MakaMekComponent.MediumLaser, 6, WeaponRange.Medium)]
    [InlineData(MakaMekComponent.MediumLaser, 9, WeaponRange.Long)]
    [InlineData(MakaMekComponent.MediumLaser, 10, WeaponRange.OutOfRange)]
    [InlineData(MakaMekComponent.AC2, 3, WeaponRange.Minimum)]
    [InlineData(MakaMekComponent.AC2, 4, WeaponRange.Minimum)]
    [InlineData(MakaMekComponent.AC2, 8, WeaponRange.Short)]
    [InlineData(MakaMekComponent.LRM5, 5, WeaponRange.Minimum)]
    [InlineData(MakaMekComponent.LRM5, 6, WeaponRange.Minimum)]
    [InlineData(MakaMekComponent.LRM5, 7, WeaponRange.Short)]
    public void GetRangeBracket_ReturnsCorrectRange(
        MakaMekComponent componentType, 
        int distance, 
        WeaponRange expectedRange)
    {
        // Arrange
        var definition = WeaponDefinitions.GetDefinitionByWeaponType(componentType);
        
        // Act
        var result = definition!.GetRangeBracket(distance);
        
        // Assert
        result.ShouldBe(expectedRange);
    }
}
