using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class FallingLevelsModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Render_ShouldFormatCorrectly()
    {
        // Arrange
        var modifier = new FallingLevelsModifier
        {
            Value = 2,
            LevelsFallen = 2
        };
        _localizationService.GetString("Modifier_FallingLevels").Returns("Falling ({0} {1}): +{2}");
        _localizationService.GetString("Levels").Returns("Levels");

        // Act
        var result = modifier.Render(_localizationService);

        // Assert
        result.ShouldBe("Falling (2 Levels): +2");
        _localizationService.Received(1).GetString("Modifier_FallingLevels");
        _localizationService.Received(1).GetString("Levels");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public void Value_ShouldReflectLevelsFallen(int levelsFallen, int expectedValue)
    {
        // Arrange & Act
        var modifier = new FallingLevelsModifier
        {
            Value = expectedValue,
            LevelsFallen = levelsFallen
        };

        // Assert
        modifier.Value.ShouldBe(expectedValue);
        modifier.LevelsFallen.ShouldBe(levelsFallen);
    }
}
