using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Data.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Ballistic;

public class MachineGunTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new MachineGun();

        // Assert
        sut.Name.ShouldBe("Machine Gun");
        sut.Size.ShouldBe(1);
        sut.Damage.ShouldBe(2);
        sut.Heat.ShouldBe(0);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(1);
        sut.MediumRange.ShouldBe(2);
        sut.LongRange.ShouldBe(3);
        sut.Type.ShouldBe(WeaponType.Ballistic);
        sut.BattleValue.ShouldBe(5);
        sut.AmmoType.ShouldBe(MakaMekComponent.ISAmmoMG);
        sut.IsDestroyed.ShouldBeFalse();
        sut.IsActive.ShouldBeTrue();
        sut.ComponentType.ShouldBe(MakaMekComponent.MachineGun);
        sut.IsAimShotCapable.ShouldBeTrue();
        sut.IsRemovable.ShouldBeTrue();
    }

    [Fact]
    public void Hit_SetsIsDestroyedToTrue()
    {
        // Arrange
        var sut = new MachineGun();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void Activate_Deactivate_TogglesIsActive()
    {
        // Arrange
        var sut = new MachineGun();

        // Act & Assert
        sut.IsActive.ShouldBeTrue(); // Default state

        sut.Deactivate();
        sut.IsActive.ShouldBeFalse();

        sut.Activate();
        sut.IsActive.ShouldBeTrue();
    }
    [Fact]
    public void CreateAmmo_Returns_CorrectAmmo()
    {
        var sut = MachineGun.CreateAmmo();
        sut.ComponentType.ShouldBe(MakaMekComponent.ISAmmoMG);
        sut.RemainingShots.ShouldBe(200);
    }
}
