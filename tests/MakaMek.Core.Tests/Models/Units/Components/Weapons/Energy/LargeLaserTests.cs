using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Energy;

public class LargeLaserTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new LargeLaser();

        // Assert
        sut.Name.ShouldBe("Large Laser");
        sut.Size.ShouldBe(2);
        sut.Heat.ShouldBe(8);
        sut.Damage.ShouldBe(8);
        sut.BattleValue.ShouldBe(123);
        sut.AmmoType.ShouldBe(null);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(5);
        sut.MediumRange.ShouldBe(10);
        sut.LongRange.ShouldBe(15);
        sut.Type.ShouldBe(WeaponType.Energy);
        sut.ComponentType.ShouldBe(MakaMekComponent.LargeLaser);
        sut.IsAimShotCapable.ShouldBeTrue();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysLargeLaser()
    {
        // Arrange
        var laser = new LargeLaser();

        // Act
        laser.Hit();

        // Assert
        laser.IsDestroyed.ShouldBeTrue();
    }
}
