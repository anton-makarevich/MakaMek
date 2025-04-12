using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal.Actuators;

public class ShoulderTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new Shoulder();

        // Assert
        sut.Name.ShouldBe("Shoulder");
        sut.MountedAtSlots.ToList().Count.ShouldBe(1);
        sut.MountedAtSlots.ShouldBe([0]);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.Shoulder);
        sut.IsRemovable.ShouldBeFalse();
    }
}
