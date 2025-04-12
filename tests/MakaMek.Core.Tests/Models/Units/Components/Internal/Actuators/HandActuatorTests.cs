using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal.Actuators;

public class HandActuatorTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new HandActuator();

        // Assert
        sut.Name.ShouldBe("Hand Actuator");
        sut.MountedAtSlots.ToList().Count.ShouldBe(1);
        sut.MountedAtSlots.ShouldBe([3]);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.HandActuator);
        sut.IsRemovable.ShouldBeTrue();
    }
}