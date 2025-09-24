using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Melee;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Melee;

public class HatchetTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Hatchet();

        // Assert
        sut.Name.ShouldBe("Hatchet");
        sut.Size.ShouldBe(1);
        sut.Heat.ShouldBe(0);
        sut.Damage.ShouldBe(0); // Damage is calculated based on mech tonnage
        sut.BattleValue.ShouldBe(5);
        sut.AmmoType.ShouldBe(null);
        sut.MinimumRange.ShouldBe(0);
        sut.ShortRange.ShouldBe(0);
        sut.MediumRange.ShouldBe(0);
        sut.LongRange.ShouldBe(0);
        sut.Type.ShouldBe(WeaponType.Melee);
        sut.ComponentType.ShouldBe(MakaMekComponent.Hatchet);
        sut.IsAimShotCapable.ShouldBeFalse();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysHatchet()
    {
        // Arrange
        var hatchet = new Hatchet();

        // Act
        hatchet.Hit();

        // Assert
        hatchet.IsDestroyed.ShouldBeTrue();
    }
}
