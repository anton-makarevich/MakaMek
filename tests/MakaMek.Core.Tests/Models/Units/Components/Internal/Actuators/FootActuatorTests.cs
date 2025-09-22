using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal.Actuators;

public class FootActuatorTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new FootActuator();

        // Assert
        sut.Name.ShouldBe("Foot Actuator");
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.FootActuator);
        sut.IsRemovable.ShouldBeFalse();
    }
    
    [Fact]
    public void DefaultMountSlots_ShouldBeCorrect()
    {
        FootActuator.DefaultMountSlots.ShouldBe([3]);
    }
}