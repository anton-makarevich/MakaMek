using Sanet.MakaMek.Core.Data.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Energy;

public class MediumLaserTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new MediumLaser();

        // Assert
        sut.Name.ShouldBe("Medium Laser");
        sut.Size.ShouldBe(1);
        sut.Heat.ShouldBe(3);
        sut.Damage.ShouldBe(5);
        sut.BattleValue.ShouldBe(46);
        sut.AmmoType.ShouldBe(null);
        sut.ComponentType.ShouldBe(MakaMekComponent.MediumLaser);
        sut.IsAimShotCapable.ShouldBeTrue();
        sut.IsRemovable.ShouldBeTrue();
    }

    [Fact]
    public void Hit_DestroysLaser()
    {
        // Arrange
        var laser = new MediumLaser();

        // Act
        laser.Hit();

        // Assert
        laser.IsDestroyed.ShouldBeTrue();
    }
}
