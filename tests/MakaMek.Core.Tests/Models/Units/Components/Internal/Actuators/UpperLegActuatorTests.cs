using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal.Actuators;

public class UpperLegActuatorTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new UpperLegActuator();

        // Assert
        sut.Name.ShouldBe("Upper Leg Actuator");
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.UpperLegActuator);
        sut.IsRemovable.ShouldBeFalse();
    }
    
    [Fact]
    public void DefaultMountSlots_ShouldBeCorrect()
    {
        UpperLegActuator.DefaultMountSlots.ShouldBe([1]);
    }
}