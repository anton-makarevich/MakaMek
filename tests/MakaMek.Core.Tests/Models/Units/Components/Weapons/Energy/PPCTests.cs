using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Energy;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Energy;

public class PpcTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Ppc();

        // Assert
        sut.Name.ShouldBe("PPC");
        sut.Size.ShouldBe(3);
        sut.Heat.ShouldBe(10);
        sut.Damage.ShouldBe(10);
        sut.BattleValue.ShouldBe(176);
        sut.AmmoType.ShouldBe(null);
        sut.MinimumRange.ShouldBe(3);
        sut.ShortRange.ShouldBe(6);
        sut.MediumRange.ShouldBe(12);
        sut.LongRange.ShouldBe(18);
        sut.Type.ShouldBe(WeaponType.Energy);
        sut.ComponentType.ShouldBe(MakaMekComponent.PPC);
        sut.IsAimShotCapable.ShouldBeTrue();
        sut.IsRemovable.ShouldBeTrue();
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysPPC()
    {
        // Arrange
        var ppc = new Ppc();

        // Act
        ppc.Hit();

        // Assert
        ppc.IsDestroyed.ShouldBeTrue();
    }
}
