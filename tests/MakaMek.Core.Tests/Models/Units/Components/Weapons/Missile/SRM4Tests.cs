using Sanet.MakaMek.Core.Data.Units;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Missile;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Missile;

public class Srm4Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Srm4();

        // Assert
        sut.Name.ShouldBe("SRM-4");
        sut.Size.ShouldBe(1);
        sut.Heat.ShouldBe(3);
        sut.Damage.ShouldBe(8); // Total damage for all missiles
        sut.BattleValue.ShouldBe(39);
        sut.AmmoType.ShouldBe(MakaMekComponent.ISAmmoSRM4);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(3);
        sut.MediumRange.ShouldBe(6);
        sut.LongRange.ShouldBe(9);
        sut.Type.ShouldBe(WeaponType.Missile);
        sut.Clusters.ShouldBe(4);
        sut.ClusterSize.ShouldBe(1);
        sut.WeaponSize.ShouldBe(4); // 4 clusters * 1 missile per cluster
        sut.ComponentType.ShouldBe(MakaMekComponent.SRM4);
        sut.IsAimShotCapable.ShouldBeFalse();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysSRM4()
    {
        // Arrange
        var sut = new Srm4();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
    
    [Fact]
    public void CreateAmmo_Returns_CorrectAmmo()
    {
        var sut = Srm4.CreateAmmo();
        sut.ComponentType.ShouldBe(MakaMekComponent.ISAmmoSRM4);
        sut.RemainingShots.ShouldBe(25);
    }
}
