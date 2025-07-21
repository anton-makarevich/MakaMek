using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;

public class FootActuatorMovementPenaltyTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new FootActuatorMovementPenalty
        {
            DestroyedCount = 2,
            Value = 2
        };
        _localizationService.GetString("Penalty_FootActuatorMovement").Returns("Foot Actuator Movement Penalty ({0} destroyed): -{1} MP");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Foot Actuator Movement Penalty (2 destroyed): -2 MP");
        _localizationService.Received(1).GetString("Penalty_FootActuatorMovement");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    public void Value_ShouldMatchDestroyedCount(int destroyedCount, int expectedValue)
    {
        // Arrange & Act
        var sut = new FootActuatorMovementPenalty
        {
            DestroyedCount = destroyedCount,
            Value = expectedValue
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
        sut.DestroyedCount.ShouldBe(destroyedCount);
    }
}
