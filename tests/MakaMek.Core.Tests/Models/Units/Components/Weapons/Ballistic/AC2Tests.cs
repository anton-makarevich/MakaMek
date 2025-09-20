using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Ballistic;

public class Ac2Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Ac2();

        // Assert
        sut.Name.ShouldBe("AC/2");
        sut.Size.ShouldBe(1);
        sut.Heat.ShouldBe(1);
        sut.Damage.ShouldBe(2);
        sut.BattleValue.ShouldBe(37);
        sut.AmmoType.ShouldBe(MakaMekComponent.ISAmmoAC2);
        sut.MinimumRange.ShouldBe(4);
        sut.ShortRange.ShouldBe(8);
        sut.MediumRange.ShouldBe(16);
        sut.LongRange.ShouldBe(24);
        sut.Type.ShouldBe(WeaponType.Ballistic);
        sut.ComponentType.ShouldBe(MakaMekComponent.AC2);
        sut.IsAimShotCapable.ShouldBeTrue();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysAC2()
    {
        // Arrange
        var sut = new Ac2();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void CreateAmmo_Returns_CorrectAmmo()
    {
        var sut = Ac2.CreateAmmo();
        sut.ComponentType.ShouldBe(MakaMekComponent.ISAmmoAC2);
        sut.RemainingShots.ShouldBe(45);
    }
}
