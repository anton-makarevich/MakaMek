using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;

public class WeaponTests
{
    private readonly Weapon _weapon = new Lrm5();

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
        var weapon = new Lrm5();
        
        // Assert
        weapon.Target.ShouldBeNull();
    }
    
    [Fact]
    public void Target_ShouldBeSettable()
    {
        // Arrange
        var weapon = new Lrm5();
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
        var weapon = new Lrm5();
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
        var weapon = new Lrm5();
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
    [InlineData(WeaponType.Energy, MakaMekComponent.ISAmmoAC5, true)] // Edge case: Energy weapon with an ammo type
    public void RequiresAmmo_ReturnsCorrectValue(WeaponType weaponType, MakaMekComponent? ammoComponentType, bool expected)
    {
        // Arrange
        var definition = CreateTestWeaponDefinition(weaponType, ammoComponentType);
        var weapon = new TestWeapon(definition);
        
        // Act & Assert
        weapon.RequiresAmmo.ShouldBe(expected);
    }
    
    private class TestWeapon : Weapon
    {
        public TestWeapon(WeaponDefinition definition) : base(definition)
        {
        }
    }
    
    private static WeaponDefinition CreateTestWeaponDefinition(WeaponType type, MakaMekComponent? ammoComponentType)
    {
        return new WeaponDefinition(
            Name: "Test Weapon",
            ElementaryDamage: 5,
            Heat: 3,
            MinimumRange: 0,
            ShortRange: 3,
            MediumRange: 6,
            LongRange: 9,
            Type: type,
            BattleValue: 10,
            Clusters: 1,
            ClusterSize: 1,
            WeaponComponentType: MakaMekComponent.MachineGun,
            AmmoComponentType: ammoComponentType);
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
