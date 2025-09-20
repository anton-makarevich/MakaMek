using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Missile;

public class Srm6Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Srm6();

        // Assert
        sut.Name.ShouldBe("SRM-6");
        sut.Size.ShouldBe(2);
        sut.Heat.ShouldBe(4);
        sut.Damage.ShouldBe(12); // Total damage for all missiles
        sut.BattleValue.ShouldBe(59);
        sut.AmmoType.ShouldBe(MakaMekComponent.ISAmmoSRM6);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(3);
        sut.MediumRange.ShouldBe(6);
        sut.LongRange.ShouldBe(9);
        sut.Type.ShouldBe(WeaponType.Missile);
        sut.Clusters.ShouldBe(6);
        sut.ClusterSize.ShouldBe(1);
        sut.WeaponSize.ShouldBe(6); // 6 clusters * 1 missile per cluster
        sut.ComponentType.ShouldBe(MakaMekComponent.SRM6);
        sut.IsAimShotCapable.ShouldBeFalse();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysSRM6()
    {
        // Arrange
        var sut = new Srm6();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void CreateAmmo_Returns_CorrectAmmo()
    {
        var sut = Srm6.CreateAmmo();
        sut.ComponentType.ShouldBe(MakaMekComponent.ISAmmoSRM6);
        sut.RemainingShots.ShouldBe(15);
    }
}
