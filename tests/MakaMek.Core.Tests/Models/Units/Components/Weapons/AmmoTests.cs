using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons;

public class AmmoTests
{
    [Theory]
    [InlineData(AmmoType.MachineGun,"MachineGun Ammo", MakaMekComponent.ISAmmoMG)]
    [InlineData(AmmoType.AC2,"AC2 Ammo", MakaMekComponent.ISAmmoAC2)]
    [InlineData(AmmoType.AC5,"AC5 Ammo", MakaMekComponent.ISAmmoAC5)]
    [InlineData(AmmoType.AC10,"AC10 Ammo", MakaMekComponent.ISAmmoAC10)]
    [InlineData(AmmoType.AC20,"AC20 Ammo", MakaMekComponent.ISAmmoAC20)]
    [InlineData(AmmoType.LRM5,"LRM5 Ammo", MakaMekComponent.ISAmmoLRM5)]
    [InlineData(AmmoType.LRM10,"LRM10 Ammo", MakaMekComponent.ISAmmoLRM10)]
    [InlineData(AmmoType.LRM15,"LRM15 Ammo", MakaMekComponent.ISAmmoLRM15)]
    [InlineData(AmmoType.LRM20,"LRM20 Ammo", MakaMekComponent.ISAmmoLRM20)]
    [InlineData(AmmoType.SRM2,"SRM2 Ammo", MakaMekComponent.ISAmmoSRM2)]
    [InlineData(AmmoType.SRM4,"SRM4 Ammo", MakaMekComponent.ISAmmoSRM4)]
    [InlineData(AmmoType.SRM6,"SRM6 Ammo", MakaMekComponent.ISAmmoSRM6)]
    public void Constructor_InitializesCorrectly(AmmoType ammoType, string name, MakaMekComponent componentType)
    {
        // Arrange & Act
        var sut = new Ammo(ammoType, 200);

        // Assert
        sut.Name.ShouldBe(name);
        sut.Type.ShouldBe(ammoType);
        sut.RemainingShots.ShouldBe(200);
        sut.MountedAtSlots.ToList().Count.ShouldBe(0);
        sut.Size.ShouldBe(1);
        sut.IsRemovable.ShouldBeTrue();
        sut.ComponentType.ShouldBe(componentType);
    }

    [Fact]
    public void UseShot_DecrementsRemainingShots()
    {
        // Arrange
        var sut = new Ammo(AmmoType.MachineGun, 200);

        // Act
        sut.UseShot();

        // Assert
        sut.RemainingShots.ShouldBe(199);
    }

    [Fact]
    public void UseShot_WhenEmpty_DoesNotDecrementBelowZero()
    {
        // Arrange
        var sut = new Ammo(AmmoType.MachineGun, 0);

        // Act
        sut.UseShot();

        // Assert
        sut.RemainingShots.ShouldBe(0);
    }

    [Fact]
    public void Hit_DestroysAmmo()
    {
        // Arrange
        var sut = new Ammo(AmmoType.MachineGun, 200);

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
}
