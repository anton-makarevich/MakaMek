using Sanet.MakaMek.Core.Data.Community;
using Shouldly;
using Sanet.MakaMek.Core.Models.Units.Components;

namespace Sanet.MakaMek.Core.Tests.Models.Units.Components;

public class HeatSinkTests
{
    [Fact]
    public void Constructor_DefaultValues()
    {
        // Arrange & Act
        var sut = new HeatSink();

        // Assert
        sut.Name.ShouldBe("Heat Sink");
        sut.MountedAtSlots.ShouldBeEmpty();
        sut.HeatDissipation.ShouldBe(1);
        sut.IsDestroyed.ShouldBeFalse();
        sut.ComponentType.ShouldBe(MakaMekComponent.HeatSink);
        sut.IsRemovable.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var sut = new HeatSink(dissipation: 2, name: "Double Heat Sink");

        // Assert
        sut.Name.ShouldBe("Double Heat Sink");
        sut.MountedAtSlots.ShouldBeEmpty();
        sut.HeatDissipation.ShouldBe(2);
        sut.IsDestroyed.ShouldBeFalse();
    }

    [Fact]
    public void Hit_DestroysHeatSink()
    {
        // Arrange
        var sut = new HeatSink();

        // Act
        sut.Hit();

        // Assert
        sut.IsDestroyed.ShouldBeTrue();
    }
}
