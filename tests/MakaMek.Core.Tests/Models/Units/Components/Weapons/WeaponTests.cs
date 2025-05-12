using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;

public class WeaponTests
{
    private readonly Weapon _weapon;

    public WeaponTests()
    {
        _weapon = new Weapon(WeaponDefinitions.LRM5);
    }

    [Theory]
    [InlineData(0, WeaponRange.OutOfRange)] // Attacker's position
    [InlineData(6, WeaponRange.Minimum)] // At minimum range
    [InlineData(7, WeaponRange.Short)] // At short range boundary
    [InlineData(10, WeaponRange.Medium)] // Within short range
    [InlineData(14, WeaponRange.Medium)] // At a medium range boundary
    [InlineData(17, WeaponRange.Long)] // Within long range
    [InlineData(21, WeaponRange.Long)] // At long range boundary
    [InlineData(22, WeaponRange.OutOfRange)] // Beyond long range
    public void GetRangeBracket_ReturnsCorrectRange(int distance, WeaponRange expectedRange)
    {
        // Act
        var result = _weapon.GetRangeBracket(distance);

        // Assert
        result.ShouldBe(expectedRange);
    }
    
    [Fact]
    public void Target_ShouldBeNull_ByDefault()
    {
        // Arrange
        var weapon = new Weapon(WeaponDefinitions.LRM5);
        
        // Assert
        weapon.Target.ShouldBeNull();
    }
    
    [Fact]
    public void Target_ShouldBeSettable()
    {
        // Arrange
        var weapon = new Weapon(WeaponDefinitions.LRM5);
        var mockUnit = new MockUnit();
        
        // Act
        weapon.Target = mockUnit;
        
        // Assert
        weapon.Target.ShouldBe(mockUnit);
    }
    
    [Fact]
    public void Target_ShouldBeResettable()
    {
        // Arrange
        var weapon = new Weapon(WeaponDefinitions.LRM5);
        var mockUnit = new MockUnit();
        weapon.Target = mockUnit;
        
        // Act
        weapon.Target = null;
        
        // Assert
        weapon.Target.ShouldBeNull();
    }
    
    [Fact]
    public void Target_CanBeChanged()
    {
        // Arrange
        var weapon = new Weapon(WeaponDefinitions.LRM5);
        var mockUnit1 = new MockUnit();
        var mockUnit2 = new MockUnit();
        
        // Act
        weapon.Target = mockUnit1;
        weapon.Target = mockUnit2;
        
        // Assert
        weapon.Target.ShouldBe(mockUnit2);
        weapon.Target.ShouldNotBe(mockUnit1);
    }
    
    [Theory]
    [InlineData(WeaponType.Energy, null, false)]
    [InlineData(WeaponType.Ballistic, MakaMekComponent.ISAmmoAC5, true)]
    [InlineData(WeaponType.Missile, MakaMekComponent.ISAmmoLRM5, true)]
    [InlineData(WeaponType.Energy, MakaMekComponent.ISAmmoAC5, true)] // Edge case: Energy weapon with ammo type
    public void RequiresAmmo_ReturnsCorrectValue(WeaponType weaponType, MakaMekComponent? ammoComponentType, bool expected)
    {
        // Arrange
        var definition = CreateTestWeaponDefinition(weaponType, ammoComponentType);
        var weapon = new Weapon(definition);
        
        // Act & Assert
        weapon.RequiresAmmo.ShouldBe(expected);
    }

    [Fact]
    public void Weapon_Properties_ReturnDefinitionValues()
    {
        // Arrange
        var definition = WeaponDefinitions.AC20;
        var weapon = new Weapon(definition);
        
        // Assert
        weapon.Damage.ShouldBe(definition.TotalDamage);
        weapon.Heat.ShouldBe(definition.Heat);
        weapon.MinimumRange.ShouldBe(definition.MinimumRange);
        weapon.ShortRange.ShouldBe(definition.ShortRange);
        weapon.MediumRange.ShouldBe(definition.MediumRange);
        weapon.LongRange.ShouldBe(definition.LongRange);
        weapon.Type.ShouldBe(definition.Type);
        weapon.Clusters.ShouldBe(definition.Clusters);
        weapon.ClusterSize.ShouldBe(definition.ClusterSize);
        weapon.WeaponSize.ShouldBe(definition.WeaponSize);
        weapon.ComponentType.ShouldBe(definition.WeaponComponentType);
        weapon.AmmoType.ShouldBe(definition.AmmoComponentType);
    }

    [Fact]
    public void Weapon_WithCustomSize_InitializesCorrectly()
    {
        // Arrange & Act
        var weapon = new Weapon(WeaponDefinitions.LRM20, 3);
        
        // Assert
        weapon.Size.ShouldBe(3);
    }
    
    private static WeaponDefinition CreateTestWeaponDefinition(WeaponType type, MakaMekComponent? ammoComponentType)
    {
        return new WeaponDefinition(
            name: "Test Weapon",
            elementaryDamage: 5,
            heat: 3,
            minimumRange: 0,
            shortRange: 3,
            mediumRange: 6,
            longRange: 9,
            type: type,
            battleValue: 10,
            clusters: 1,
            clusterSize: 1,
            weaponComponentType: MakaMekComponent.MediumLaser,
            ammoComponentType: ammoComponentType);
    }
    
    private class MockUnit : Unit
    {
        public MockUnit() : base("Mock", "Unit", 20, 4, [])
        {
        }
        
        public override int CalculateBattleValue() => 0;
        
        public override bool CanMoveBackward(MovementType type) => true;
        protected override void ApplyHeatEffects()
        {
            throw new NotImplementedException();
        }

        public override PartLocation? GetTransferLocation(PartLocation location) => null;
        public override LocationCriticalHitsData CalculateCriticalHitsData(PartLocation location, IDiceRoller diceRoller)
        {
            throw new NotImplementedException();
        }
    }
}
