using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;

public class WeaponTests
{
    private readonly Weapon _weapon;

    public WeaponTests()
    {
        _weapon = new Lrm5();
    }

    [Theory]
    [InlineData(0, WeaponRange.OutOfRange)] // Attacker's position
    [InlineData(6, WeaponRange.Minimum)] // At minimum range
    [InlineData(7, WeaponRange.Short)] // At short range boundary
    [InlineData(10, WeaponRange.Medium)] // Within short range
    [InlineData(14, WeaponRange.Medium)] // At medium range boundary
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
    [InlineData(WeaponType.Energy, AmmoType.None, false)]
    [InlineData(WeaponType.Ballistic, AmmoType.AC5, true)]
    [InlineData(WeaponType.Missile, AmmoType.LRM5, true)]
    [InlineData(WeaponType.Energy, AmmoType.AC5, true)] // Edge case: Energy weapon with ammo type
    public void RequiresAmmo_ReturnsCorrectValue(WeaponType weaponType, AmmoType ammoType, bool expected)
    {
        // Arrange
        var weapon = new TestWeapon(weaponType, ammoType);
        
        // Act & Assert
        weapon.RequiresAmmo.ShouldBe(expected);
    }
    
    private class TestWeapon(WeaponType type, AmmoType ammoType)
        : Weapon("Test Weapon", 5, 3, 0, 3, 6, 9, type, 10, 1, 1, 1, ammoType)
    {
        public override MakaMekComponent ComponentType => throw new NotImplementedException();
    }
    
    private class MockUnit : Unit
    {
        public MockUnit() : base("Mock", "Unit", 20, 4, Array.Empty<UnitPart>())
        {
        }
        
        public override int CalculateBattleValue() => 0;
        
        public override bool CanMoveBackward(MovementType type) => true;
        protected override void ApplyHeatEffects()
        {
            throw new NotImplementedException();
        }

        public override PartLocation? GetTransferLocation(PartLocation location) => null;
    }
}
