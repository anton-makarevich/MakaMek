using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal;

public class CockpitTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Cockpit();

        // Assert
        sut.Name.ShouldBe("Cockpit");
        sut.MountedAtSlots.ToList().Count.ShouldBe(1);
        sut.MountedAtSlots.ShouldBe([2]);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.Cockpit);
        sut.IsRemovable.ShouldBeFalse();
    }
}