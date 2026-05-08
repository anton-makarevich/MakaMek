using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;

public class WeaponTests
{
    private readonly Weapon _weapon = new Lrm5();

    [Theory]
    [InlineData(0, RangeBracket.OutOfRange)] // Attacker's position
    [InlineData(6, RangeBracket.Minimum)] // At minimum rangeBracket
    [InlineData(7, RangeBracket.Short)] // At short rangeBracket boundary
    [InlineData(10, RangeBracket.Medium)] // Within short rangeBracket
    [InlineData(14, RangeBracket.Medium)] // At a medium rangeBracket boundary
    [InlineData(17, RangeBracket.Long)] // Within long rangeBracket
    [InlineData(21, RangeBracket.Long)] // At long rangeBracket boundary
    [InlineData(22, RangeBracket.OutOfRange)] // Beyond long rangeBracket
    public void GetRangeBracket_ReturnsCorrectRange(int distance, RangeBracket expectedRangeBracket)
    {
        // Act
        var result = _weapon.Range!.GetRangeBracket(distance);

        // Assert
        result.ShouldBe(expectedRangeBracket);
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
    
    private class TestWeapon(WeaponDefinition definition) : Weapon(definition);
    
    private static WeaponDefinition CreateTestWeaponDefinition(WeaponType type, MakaMekComponent? ammoComponentType)
    {
        return new WeaponDefinition(
            Name: "Test Weapon",
            ElementaryDamage: 5,
            Heat: 3,
            Range: new WeaponRange(0, 3, 6, 9),
            Type: type,
            BattleValue: 10,
            Clusters: 1,
            ClusterSize: 1,
            WeaponComponentType: MakaMekComponent.MachineGun,
            AmmoComponentType: ammoComponentType);
    }
    
    [Fact]
    public void GetFiringArcs_ShouldReturnCorrectArcs_BasedOnMountingLocation()
    {
        var legWeapon = new TestWeapon(CreateTestWeaponDefinition(WeaponType.Energy, null));
        var part = new Leg("Leg", PartLocation.LeftLeg, 8, 4);
        part.TryAddComponent(legWeapon).ShouldBeTrue();
        
        var torsoWeapon = new TestWeapon(CreateTestWeaponDefinition(WeaponType.Energy, null));
        var torso = new CenterTorso("CenterTorso", 10, 2, 6);
        torso.TryAddComponent(torsoWeapon).ShouldBeTrue();
        
        var armWeapon = new TestWeapon(CreateTestWeaponDefinition(WeaponType.Energy, null));
        var arm = new Arm("Arm", PartLocation.LeftArm, 8, 4);
        arm.TryAddComponent(armWeapon).ShouldBeTrue();
        
        var mech = new Mech("Test", "TST-1A", 4, new List<UnitPart> { torso, part, arm });
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        mech.Deploy(position, null);
        
        legWeapon.GetFiringArcs().ShouldBe([FiringArc.Front]);
        torsoWeapon.GetFiringArcs().ShouldBe([FiringArc.Front]);
        armWeapon.GetFiringArcs().ShouldBe([FiringArc.Front, FiringArc.Left]);
    }
    
    [Fact]
    public void GetFiringArcs_ShouldReturnEmptyList_WhenNotMounted()
    {
        var sut = new TestWeapon(CreateTestWeaponDefinition(WeaponType.Energy, null));
        
        sut.GetFiringArcs().ShouldBeEmpty();
    }
}
