using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;

public class UpperLegActuatorMovementPenaltyTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new UpperLegActuatorMovementPenalty
        {
            DestroyedCount = 2,
            Value = 2
        };
        _localizationService.GetString("Penalty_UpperLegActuatorMovement").Returns("Upper Leg Actuator Movement Penalty ({0} destroyed): -{1} MP");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Upper Leg Actuator Movement Penalty (2 destroyed): -2 MP");
        _localizationService.Received(1).GetString("Penalty_UpperLegActuatorMovement");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    public void Value_ShouldMatchDestroyedCount(int destroyedCount, int expectedValue)
    {
        // Arrange & Act
        var sut = new UpperLegActuatorMovementPenalty
        {
            DestroyedCount = destroyedCount,
            Value = expectedValue
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
        sut.DestroyedCount.ShouldBe(destroyedCount);
    }
}
