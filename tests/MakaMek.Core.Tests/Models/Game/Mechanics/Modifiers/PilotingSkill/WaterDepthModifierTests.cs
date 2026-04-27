using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class WaterDepthModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new WaterDepthModifier
        {
            Value = 2,
            WaterDepth = 1
        };
        _localizationService.GetString("Modifier_WaterDepth").Returns("Water Depth {0}: +{1}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Water Depth 1: +2");
        _localizationService.Received(1).GetString("Modifier_WaterDepth");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(4, 4)]
    public void WaterDepth_ShouldBeSetCorrectly(int waterDepth, int expectedWaterDepth)
    {
        // Arrange & Act
        var sut = new WaterDepthModifier
        {
            Value = 2,
            WaterDepth = waterDepth
        };

        // Assert
        sut.WaterDepth.ShouldBe(expectedWaterDepth);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void Value_ShouldBeSetCorrectly(int value, int expectedValue)
    {
        // Arrange & Act
        var sut = new WaterDepthModifier
        {
            Value = value,
            WaterDepth = 1
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    public void Render_WithDifferentDepthsAndValues_ShouldFormatCorrectly(int waterDepth, int value)
    {
        // Arrange
        var sut = new WaterDepthModifier
        {
            Value = value,
            WaterDepth = waterDepth
        };
        _localizationService.GetString("Modifier_WaterDepth").Returns("Water Depth {0}: +{1}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe($"Water Depth {waterDepth}: +{value}");
    }
}
