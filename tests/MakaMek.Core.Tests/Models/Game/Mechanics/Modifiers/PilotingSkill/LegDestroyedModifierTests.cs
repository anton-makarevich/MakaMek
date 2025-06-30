using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class LegDestroyedModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var sut = new LegDestroyedModifier
        {
            Value = 5
        };
        _localizationService.GetString("Modifier_LegDestroyed").Returns("Leg Destroyed: +{0}");

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Leg Destroyed: +5");
        _localizationService.Received(1).GetString("Modifier_LegDestroyed");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void Value_ShouldBeSetCorrectly(int expectedValue)
    {
        // Arrange & Act
        var sut = new LegDestroyedModifier
        {
            Value = expectedValue
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
    }
}
