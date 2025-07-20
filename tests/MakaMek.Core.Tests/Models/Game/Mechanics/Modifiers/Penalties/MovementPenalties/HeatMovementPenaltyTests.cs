using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;

public class HeatMovementPenaltyTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new HeatMovementPenalty
        {
            HeatLevel = 15,
            Value = 3
        };
        _localizationService.GetString("Penalty_HeatMovement").Returns("Heat Movement Penalty (Heat: {0}): -{1} MP");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Heat Movement Penalty (Heat: 15): -3 MP");
        _localizationService.Received(1).GetString("Penalty_HeatMovement");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(10, 2)]
    [InlineData(15, 3)]
    [InlineData(20, 4)]
    [InlineData(25, 5)]
    [InlineData(30, 5)]
    public void Value_ShouldBeSetCorrectly(int heatLevel, int expectedValue)
    {
        // Arrange & Act
        var sut = new HeatMovementPenalty
        {
            HeatLevel = heatLevel,
            Value = expectedValue
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
        sut.HeatLevel.ShouldBe(heatLevel);
    }
}
