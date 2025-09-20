using Sanet.MakaMek.Core.Data.Units.Components;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components.Internal;

public class LifeSupportTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new LifeSupport();

        // Assert
        sut.Name.ShouldBe("Life Support");
        sut.MountedAtSlots.ToList().Count.ShouldBe(2);
        sut.MountedAtSlots.ShouldBe([0, 5]);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.LifeSupport);
        sut.IsRemovable.ShouldBeFalse();
    }
}
