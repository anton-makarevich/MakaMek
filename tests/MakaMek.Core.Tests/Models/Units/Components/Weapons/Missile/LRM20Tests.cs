using Sanet.MakaMek.Core.Data.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Missile;

public class Lrm20Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Lrm20();

        // Assert
        sut.Name.ShouldBe("LRM-20");
        sut.Size.ShouldBe(5);
        sut.Heat.ShouldBe(6);
        sut.Damage.ShouldBe(20); // Total damage for all missiles
        sut.BattleValue.ShouldBe(181);
        sut.AmmoType.ShouldBe(MakaMekComponent.ISAmmoLRM20);
        sut.MinimumRange.ShouldBe(6);
        sut.ShortRange.ShouldBe(7);
        sut.MediumRange.ShouldBe(14);
        sut.LongRange.ShouldBe(21);
        sut.Type.ShouldBe(WeaponType.Missile);
        sut.Clusters.ShouldBe(4);
        sut.ClusterSize.ShouldBe(5);
        sut.WeaponSize.ShouldBe(20); // 4 clusters * 5 missiles per cluster
        sut.ComponentType.ShouldBe(MakaMekComponent.LRM20);
        sut.IsAimShotCapable.ShouldBeFalse();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysLRM20()
    {
        // Arrange
        var sut = new Lrm20();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void CreateAmmo_Returns_CorrectAmmo()
    {
        var sut = Lrm20.CreateAmmo();
        sut.ComponentType.ShouldBe(MakaMekComponent.ISAmmoLRM20);
        sut.RemainingShots.ShouldBe(6);
    }
}
