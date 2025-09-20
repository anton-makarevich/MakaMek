using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Ballistic;

public class Ac20Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Ac20();

        // Assert
        sut.Name.ShouldBe("AC/20");
        sut.Size.ShouldBe(10);
        sut.Heat.ShouldBe(7);
        sut.Damage.ShouldBe(20);
        sut.BattleValue.ShouldBe(178);
        sut.AmmoType.ShouldBe(MakaMekComponent.ISAmmoAC20);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(3);
        sut.MediumRange.ShouldBe(6);
        sut.LongRange.ShouldBe(9);
        sut.Type.ShouldBe(WeaponType.Ballistic);
        sut.ComponentType.ShouldBe(MakaMekComponent.AC20);
        sut.IsAimShotCapable.ShouldBeTrue();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysAC20()
    {
        // Arrange
        var sut = new Ac20();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void CreateAmmo_Returns_CorrectAmmo()
    {
        var sut = Ac20.CreateAmmo();
        sut.ComponentType.ShouldBe(MakaMekComponent.ISAmmoAC20);
        sut.RemainingShots.ShouldBe(5);
    }
}
