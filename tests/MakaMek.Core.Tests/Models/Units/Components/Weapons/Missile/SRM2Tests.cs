using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Missile;

public class SRM2Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Srm2();

        // Assert
        sut.Name.ShouldBe("SRM-2");
        sut.Size.ShouldBe(1);
        sut.Heat.ShouldBe(1);
        sut.Damage.ShouldBe(4); // Total damage for all missiles
        sut.BattleValue.ShouldBe(25);
        sut.AmmoType.ShouldBe(AmmoType.SRM2);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(3);
        sut.MediumRange.ShouldBe(6);
        sut.LongRange.ShouldBe(9);
        sut.Clusters.ShouldBe(2);
        sut.ClusterSize.ShouldBe(1);
        sut.WeaponSize.ShouldBe(2); // 2 clusters * 1 missile per cluster
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.SRM2);
        sut.IsRemovable.ShouldBeTrue();
    }

    [Fact]
    public void Hit_DestroysSRM2()
    {
        // Arrange
        var srm2 = new Srm2();

        // Act
        srm2.Hit();

        // Assert
        srm2.IsDestroyed.ShouldBeTrue();
    }
}
