using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Localization;
using Shouldly;
using Xunit;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics.PilotingSkillRollContexts;

public class EnteringDeepWaterRollContextTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();

    [Fact]
    public void Constructor_WhenCalled_SetsWaterDepth()
    {
        // Arrange
        var waterDepth = 3;

        // Act
        var sut = new EnteringDeepWaterRollContext(waterDepth);

        // Assert
        sut.WaterDepth.ShouldBe(waterDepth);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsRollTypeToWaterEntry()
    {
        // Arrange
        var waterDepth = 2;

        // Act
        var sut = new EnteringDeepWaterRollContext(waterDepth);

        // Assert
        sut.RollType.ShouldBe(PilotingSkillRollType.WaterEntry);
    }

    [Fact]
    public void Render_WhenCalled_ReturnsLocalizedStringWithDepth()
    {
        // Arrange
        var sut = new EnteringDeepWaterRollContext(3);

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Water Entry (Depth 3)");
    }

    [Theory]
    [InlineData(1, "Water Entry (Depth 1)")]
    [InlineData(2, "Water Entry (Depth 2)")]
    [InlineData(5, "Water Entry (Depth 5)")]
    [InlineData(10, "Water Entry (Depth 10)")]
    public void Render_WithDifferentDepths_ReturnsCorrectLocalizedString(int waterDepth, string expected)
    {
        // Arrange
        var sut = new EnteringDeepWaterRollContext(waterDepth);

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe(expected);
    }
}
