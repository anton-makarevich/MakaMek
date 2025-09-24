using Sanet.MakaMek.Core.Data.Units.Components;
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
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.Cockpit);
        sut.IsRemovable.ShouldBeFalse();
    }
    
    [Fact]
    public void DefaultMountSlots_ShouldBeCorrect()
    {
        Cockpit.DefaultMountSlots.ShouldBe([2]);
    }
}