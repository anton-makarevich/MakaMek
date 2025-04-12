using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components;

public class MascTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Masc("MASC");

        // Assert
        sut.Name.ShouldBe("MASC");
        sut.Size.ShouldBe(1);
        sut.IsDestroyed.ShouldBeFalse();
        sut.IsActive.ShouldBeFalse(); // MASC starts deactivated
        sut.ComponentType.ShouldBe(MakaMekComponent.Masc);
        sut.IsRemovable.ShouldBeTrue();
    }

    [Fact]
    public void Hit_DestroysAndDeactivatesComponent()
    {
        // Arrange
        var sut = new Masc("MASC");
        sut.Activate();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
        sut.IsActive.ShouldBeFalse();
    }
}
