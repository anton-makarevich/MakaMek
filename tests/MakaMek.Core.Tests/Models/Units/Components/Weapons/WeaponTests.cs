using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Mechs;
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
    
    [Fact]
    public void Facing_ShouldMatchFirstMountPartFacing()
    {
        var sut = new TestWeapon(CreateTestWeaponDefinition(WeaponType.Energy, null));
        var part = new Leg("Leg", PartLocation.LeftLeg, 8, 4);
        part.TryAddComponent(sut).ShouldBeTrue();
        
        sut.Facing.ShouldBeNull();
        
        var mech = new Mech("Test", "TST-1A", 4, new List<UnitPart> { part });
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        mech.Deploy(position);
        
        sut.Facing.ShouldBe(position.Facing);
    }
    
    [Fact]
    public void Facing_ShouldBeNull_WhenNotMounted()
    {
        var sut = new TestWeapon(CreateTestWeaponDefinition(WeaponType.Energy, null));
        
        sut.Facing.ShouldBeNull();
    }

    [Fact]
    public void Facing_ShouldMatchTorsoFacing_WhenMountedNotOnLegs()
    {
        var sut = new TestWeapon(CreateTestWeaponDefinition(WeaponType.Energy, null));
        var part = new CenterTorso("CenterTorso", 10, 2, 6);
        part.TryAddComponent(sut).ShouldBeTrue();
        
        var mech = new Mech("Test", "TST-1A", 4, new List<UnitPart> { part });
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        mech.Deploy(position);
        mech.RotateTorso(HexDirection.TopRight);
        
        sut.Facing.ShouldBe(HexDirection.TopRight);
    }
    
    [Fact]
    public void Facing_ShouldMatchPositionFacing_WhenMountedOnLegs()
    {
        var sut = new TestWeapon(CreateTestWeaponDefinition(WeaponType.Energy, null));
        var part = new Leg("Leg", PartLocation.LeftLeg, 8, 4);
        var torso = new CenterTorso("CenterTorso", 10, 2, 6);
        part.TryAddComponent(sut).ShouldBeTrue();
        
        var mech = new Mech("Test", "TST-1A", 4, new List<UnitPart> { torso, part });
        var position = new HexPosition(new HexCoordinates(0, 0), HexDirection.Top);
        mech.Deploy(position);
        mech.RotateTorso(HexDirection.TopRight);
        
        sut.Facing.ShouldBe(HexDirection.Top);
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
        mech.Deploy(position);
        
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
