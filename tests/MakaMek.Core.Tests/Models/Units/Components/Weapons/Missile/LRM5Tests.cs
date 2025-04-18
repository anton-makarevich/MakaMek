using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Missile;

public class Lrm5Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Lrm5();

        // Assert
        sut.Name.ShouldBe("LRM-5");
        sut.Size.ShouldBe(1);
        sut.Heat.ShouldBe(2);
        sut.Damage.ShouldBe(5); // Total damage for all missiles
        sut.BattleValue.ShouldBe(45);
        sut.AmmoType.ShouldBe(AmmoType.LRM5);
        sut.Clusters.ShouldBe(1);
        sut.ClusterSize.ShouldBe(5);
        sut.WeaponSize.ShouldBe(5); // 1 cluster * 5 missiles per cluster
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.LRM5);
        sut.IsRemovable.ShouldBeTrue();
    }

    [Fact]
    public void Hit_DestroysLRM5()
    {
        // Arrange
        var sut = new Lrm5();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
}
