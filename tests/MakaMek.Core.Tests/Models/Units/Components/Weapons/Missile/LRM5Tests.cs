using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Missile;

public class LRM5Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var lrm5 = new Lrm5();

        // Assert
        lrm5.Name.ShouldBe("LRM-5");
        lrm5.Size.ShouldBe(1);
        lrm5.Heat.ShouldBe(2);
        lrm5.Damage.ShouldBe(5); // Total damage for all missiles
        lrm5.BattleValue.ShouldBe(45);
        lrm5.AmmoType.ShouldBe(AmmoType.LRM5);
        lrm5.Clusters.ShouldBe(1);
        lrm5.ClusterSize.ShouldBe(5);
        lrm5.WeaponSize.ShouldBe(5); // 1 cluster * 5 missiles per cluster
        lrm5.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysLRM5()
    {
        // Arrange
        var lrm5 = new Lrm5();

        // Act
        lrm5.Hit();

        // Assert
        lrm5.IsDestroyed.ShouldBeTrue();
    }
}
