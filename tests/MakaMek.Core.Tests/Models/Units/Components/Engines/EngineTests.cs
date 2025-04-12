using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Engines;

public class EngineTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Engine(100);

        // Assert
        sut.Name.ShouldBe("Fusion Engine 100");
        sut.Rating.ShouldBe(100);
        sut.MountedAtSlots.ToList().Count.ShouldBe(6);
        sut.MountedAtSlots.ShouldBe([0,1,2,7,8,9]);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.Engine);
        sut.IsRemovable.ShouldBeTrue();
    }
}
