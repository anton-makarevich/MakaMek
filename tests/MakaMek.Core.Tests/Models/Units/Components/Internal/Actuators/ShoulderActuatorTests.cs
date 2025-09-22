using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal.Actuators;

public class ShoulderActuatorTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new ShoulderActuator();

        // Assert
        sut.Name.ShouldBe("Shoulder");
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.Shoulder);
        sut.IsRemovable.ShouldBeFalse();
    }
    
    [Fact]
    public void DefaultMountSlots_ShouldBeCorrect()
    {
        ShoulderActuator.DefaultMountSlots.ShouldBe([0]);
    }
}
