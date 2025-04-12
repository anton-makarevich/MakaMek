using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Missile;

public class Lrm10Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Lrm10();

        // Assert
        sut.Name.ShouldBe("LRM-10");
        sut.Size.ShouldBe(1);
        sut.Heat.ShouldBe(4);
        sut.Damage.ShouldBe(10); // Total damage for all missiles
        sut.BattleValue.ShouldBe(90);
        sut.AmmoType.ShouldBe(AmmoType.LRM10);
        sut.Clusters.ShouldBe(2);
        sut.ClusterSize.ShouldBe(5);
        sut.WeaponSize.ShouldBe(10); // 2 clusters * 5 missiles per cluster
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.LRM10);
        sut.IsRemovable.ShouldBeTrue();
    }

    [Fact]
    public void Hit_DestroysLRM10()
    {
        // Arrange
        var sut = new Lrm10();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
}
