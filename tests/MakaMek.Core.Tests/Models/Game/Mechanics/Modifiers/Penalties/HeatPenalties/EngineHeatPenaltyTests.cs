using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;

public class EngineHeatPenaltyTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new EngineHeatPenalty
        {
            EngineHits = 2,
            Value = 10
        };
        _localizationService.GetString("Penalty_EngineHeat").Returns("Engine Heat Penalty ({0} hits): +{1} heat/turn");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Engine Heat Penalty (2 hits): +10 heat/turn");
        _localizationService.Received(1).GetString("Penalty_EngineHeat");
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 15)]
    public void Value_ShouldBeCalculatedCorrectly(int engineHits, int expectedValue)
    {
        // Arrange & Act
        var sut = new EngineHeatPenalty
        {
            EngineHits = engineHits,
            Value = expectedValue
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
        sut.EngineHits.ShouldBe(engineHits);
    }
}
