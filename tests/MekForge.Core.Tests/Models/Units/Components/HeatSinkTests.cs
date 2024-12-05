using FluentAssertions;
using Sanet.MekForge.Core.Models.Units.Components;
using Xunit;

namespace Sanet.MekForge.Core.Tests.Models.Units.Components;

public class HeatSinkTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        // Arrange & Act
        var heatSink = new HeatSink(dissipation: 2, name: "Double Heat Sink");

        // Assert
        heatSink.Name.Should().Be("Double Heat Sink");
        heatSink.Slots.Should().Be(1);
        heatSink.HeatDissipation.Should().Be(2);
        heatSink.IsDestroyed.Should().BeFalse();
    }

    [Fact]
    public void Constructor_DefaultValues()
    {
        // Arrange & Act
        var heatSink = new HeatSink();

        // Assert
        heatSink.Name.Should().Be("Heat Sink");
        heatSink.Slots.Should().Be(1);
        heatSink.HeatDissipation.Should().Be(1);
        heatSink.IsDestroyed.Should().BeFalse();
    }

    [Fact]
    public void ApplyDamage_DestroysHeatSink()
    {
        // Arrange
        var heatSink = new HeatSink();

        // Act
        heatSink.ApplyDamage();

        // Assert
        heatSink.IsDestroyed.Should().BeTrue();
    }
}
