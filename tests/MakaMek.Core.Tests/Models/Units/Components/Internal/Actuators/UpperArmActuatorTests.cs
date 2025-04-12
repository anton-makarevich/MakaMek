using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal.Actuators;

public class UpperArmActuatorTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new UpperArmActuator();

        // Assert
        sut.Name.ShouldBe("Upper Arm Actuator");
        sut.MountedAtSlots.ToList().Count.ShouldBe(1);
        sut.MountedAtSlots.ShouldBe([1]);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.UpperArmActuator);
        sut.IsRemovable.ShouldBeTrue();
    }
}