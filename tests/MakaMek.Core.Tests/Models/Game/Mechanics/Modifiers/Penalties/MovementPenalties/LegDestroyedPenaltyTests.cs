using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;

public class LegDestroyedPenaltyTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Theory]
    [InlineData(0, 10, null)]
    [InlineData(1, 10, 9)]
    [InlineData(2, 10, 10)]
    public void Create_ReturnsCorrectPenaltyOrNull(int destroyedLegCount, int baseWalkingMp, int? expectedValue)
    {
        // Act
        var result = LegDestroyedPenalty.Create(destroyedLegCount, baseWalkingMp);

        // Assert
        if (expectedValue == null)
        {
            result.ShouldBeNull();
        }
        else
        {
            result.ShouldNotBeNull();
            result.DestroyedLegCount.ShouldBe(destroyedLegCount > 2 ? 2 : destroyedLegCount);
            result.BaseWalkingMp.ShouldBe(baseWalkingMp);
            result.Value.ShouldBe(expectedValue.Value);
        }
    }

    [Theory]
    [InlineData(0, 10, "")]
    [InlineData(1, 10, "Leg Destroyed: -9 MP")]
    [InlineData(2, 10, "Both Legs Destroyed!")]
    public void Render_ReturnsCorrectString(int destroyedLegCount, int baseWalkingMp, string expectedResult)
    {
        // Arrange
        var penalty = LegDestroyedPenalty.Create(destroyedLegCount, baseWalkingMp);
        if (penalty == null && destroyedLegCount == 0)
        {
            // Special case for 0 legs destroyed - no penalty
            return;
        }
        
        _localizationService.GetString("Penalty_LegDestroyed_Single").Returns("Leg Destroyed: -{0} MP");
        _localizationService.GetString("Penalty_LegDestroyed_Both").Returns("Both Legs Destroyed!");

        // Act
        var result = penalty!.Render(_localizationService);

        // Assert
        result.ShouldBe(expectedResult);
        
        // Verify correct localization strings were requested
        if (destroyedLegCount == 1)
        {
            _localizationService.Received(1).GetString("Penalty_LegDestroyed_Single");
        }
        else if (destroyedLegCount >= 2)
        {
            _localizationService.Received(1).GetString("Penalty_LegDestroyed_Both");
        }
    }
}
