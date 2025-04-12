using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons.Ballistic;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Weapons.Ballistic;

public class Ac5Tests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var ac5 = new Ac5();

        // Assert
        ac5.Name.ShouldBe("AC/5");
        ac5.Size.ShouldBe(4);
        ac5.Heat.ShouldBe(1);
        ac5.Damage.ShouldBe(5);
        ac5.BattleValue.ShouldBe(123);
        ac5.AmmoType.ShouldBe(AmmoType.AC5);
        ac5.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysAC5()
    {
        // Arrange
        var ac5 = new Ac5();

        // Act
        ac5.Hit();

        // Assert
        ac5.IsDestroyed.ShouldBeTrue();
    }
}
