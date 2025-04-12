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
        var lrm10 = new Lrm10();

        // Assert
        lrm10.Name.ShouldBe("LRM-10");
        lrm10.Size.ShouldBe(1);
        lrm10.Heat.ShouldBe(4);
        lrm10.Damage.ShouldBe(10); // Total damage for all missiles
        lrm10.BattleValue.ShouldBe(90);
        lrm10.AmmoType.ShouldBe(AmmoType.LRM10);
        lrm10.Clusters.ShouldBe(2);
        lrm10.ClusterSize.ShouldBe(5);
        lrm10.WeaponSize.ShouldBe(10); // 2 clusters * 5 missiles per cluster
        lrm10.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysLRM10()
    {
        // Arrange
        var lrm10 = new Lrm10();

        // Act
        lrm10.Hit();

        // Assert
        lrm10.IsDestroyed.ShouldBeTrue();
    }
}
