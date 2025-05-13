using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Ballistic;

public class Ac5Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Ac5();

        // Assert
        sut.Name.ShouldBe("AC/5");
        sut.Size.ShouldBe(4);
        sut.Heat.ShouldBe(1);
        sut.Damage.ShouldBe(5);
        sut.BattleValue.ShouldBe(123);
        sut.AmmoType.ShouldBe(MakaMekComponent.ISAmmoAC5);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.AC5);
        sut.IsRemovable.ShouldBeTrue();
    }

    [Fact]
    public void Hit_DestroysAC5()
    {
        // Arrange
        var sut = new Ac5();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
}
