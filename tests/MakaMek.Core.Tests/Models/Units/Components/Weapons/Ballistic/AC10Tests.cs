using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Ballistic;

public class Ac10Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Ac10();

        // Assert
        sut.Name.ShouldBe("AC/10");
        sut.Size.ShouldBe(7);
        sut.Heat.ShouldBe(3);
        sut.Damage.ShouldBe(10);
        sut.BattleValue.ShouldBe(123);
        sut.AmmoType.ShouldBe(MakaMekComponent.ISAmmoAC10);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(5);
        sut.MediumRange.ShouldBe(10);
        sut.LongRange.ShouldBe(15);
        sut.Type.ShouldBe(WeaponType.Ballistic);
        sut.ComponentType.ShouldBe(MakaMekComponent.AC10);
        sut.IsAimShotCapable.ShouldBeTrue();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysAC10()
    {
        // Arrange
        var sut = new Ac10();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }

    [Fact]
    public void CreateAmmo_Returns_CorrectAmmo()
    {
        var sut = Ac10.CreateAmmo();
        sut.ComponentType.ShouldBe(MakaMekComponent.ISAmmoAC10);
        sut.RemainingShots.ShouldBe(10);
    }
}
