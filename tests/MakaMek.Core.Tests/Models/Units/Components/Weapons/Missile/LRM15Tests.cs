using Sanet.MakaMek.Core.Data.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Missile;

public class Lrm15Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Lrm15();

        // Assert
        sut.Name.ShouldBe("LRM-15");
        sut.Size.ShouldBe(3);
        sut.Heat.ShouldBe(5);
        sut.Damage.ShouldBe(15); // Total damage for all missiles
        sut.BattleValue.ShouldBe(136);
        sut.AmmoType.ShouldBe(MakaMekComponent.ISAmmoLRM15);
        sut.MinimumRange.ShouldBe(6);
        sut.ShortRange.ShouldBe(7);
        sut.MediumRange.ShouldBe(14);
        sut.LongRange.ShouldBe(21);
        sut.Type.ShouldBe(WeaponType.Missile);
        sut.Clusters.ShouldBe(3);
        sut.ClusterSize.ShouldBe(5);
        sut.WeaponSize.ShouldBe(15); // 3 clusters * 5 missiles per cluster
        sut.ComponentType.ShouldBe(MakaMekComponent.LRM15);
        sut.IsAimShotCapable.ShouldBeFalse();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysLRM15()
    {
        // Arrange
        var sut = new Lrm15();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void CreateAmmo_Returns_CorrectAmmo()
    {
        var sut = Lrm15.CreateAmmo();
        sut.ComponentType.ShouldBe(MakaMekComponent.ISAmmoLRM15);
        sut.RemainingShots.ShouldBe(8);
    }
}
