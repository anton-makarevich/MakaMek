using Sanet.MakaMek.Core.Data.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Energy;

public class SmallLaserTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new SmallLaser();

        // Assert
        sut.Name.ShouldBe("Small Laser");
        sut.Size.ShouldBe(1);
        sut.Heat.ShouldBe(1);
        sut.Damage.ShouldBe(3);
        sut.BattleValue.ShouldBe(9);
        sut.AmmoType.ShouldBe(null);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(1);
        sut.MediumRange.ShouldBe(2);
        sut.LongRange.ShouldBe(3);
        sut.Type.ShouldBe(WeaponType.Energy);
        sut.ComponentType.ShouldBe(MakaMekComponent.SmallLaser);
        sut.IsAimShotCapable.ShouldBeTrue();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysSmallLaser()
    {
        // Arrange
        var laser = new SmallLaser();

        // Act
        laser.Hit();

        // Assert
        laser.IsDestroyed.ShouldBeTrue();
    }
}
