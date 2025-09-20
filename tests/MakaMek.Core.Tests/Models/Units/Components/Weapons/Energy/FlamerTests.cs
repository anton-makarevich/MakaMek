using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Energy;

public class FlamerTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Flamer();

        // Assert
        sut.Name.ShouldBe("Flamer");
        sut.Size.ShouldBe(1);
        sut.Heat.ShouldBe(3);
        sut.Damage.ShouldBe(2);
        sut.BattleValue.ShouldBe(6);
        sut.AmmoType.ShouldBe(null);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(1);
        sut.MediumRange.ShouldBe(2);
        sut.LongRange.ShouldBe(3);
        sut.Type.ShouldBe(WeaponType.Energy);
        sut.ComponentType.ShouldBe(MakaMekComponent.Flamer);
        sut.IsAimShotCapable.ShouldBeTrue();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysFlamer()
    {
        // Arrange
        var flamer = new Flamer();

        // Act
        flamer.Hit();

        // Assert
        flamer.IsDestroyed.ShouldBeTrue();
    }
}
