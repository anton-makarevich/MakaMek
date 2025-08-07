using Sanet.MakaMek.Core.Data.Community;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Missile;

public class Srm2Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Srm2();

        // Assert
        sut.Name.ShouldBe("SRM-2");
        sut.Size.ShouldBe(1);
        sut.Heat.ShouldBe(2);
        sut.Damage.ShouldBe(4); // Total damage for all missiles
        sut.BattleValue.ShouldBe(15);
        sut.AmmoType.ShouldBe(MakaMekComponent.ISAmmoSRM2);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(3);
        sut.MediumRange.ShouldBe(6);
        sut.LongRange.ShouldBe(9);
        sut.Clusters.ShouldBe(2);
        sut.ClusterSize.ShouldBe(1);
        sut.WeaponSize.ShouldBe(2); // 2 clusters * 1 missile per cluster
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.SRM2);
        sut.IsAimShotCapable.ShouldBeFalse();
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
    
    [Fact]
    public void CreateAmmo_Returns_CorrectAmmo()
    {
        var sut = Srm2.CreateAmmo();
        sut.ComponentType.ShouldBe(MakaMekComponent.ISAmmoSRM2);
        sut.RemainingShots.ShouldBe(50);
    }
}
