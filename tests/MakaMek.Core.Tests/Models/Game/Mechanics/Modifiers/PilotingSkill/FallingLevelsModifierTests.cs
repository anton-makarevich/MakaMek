using NSubstitute;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Services.Localization;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Game.Mechanics.Modifiers.PilotingSkill;

public class FallingLevelsModifierTests
{
    private readonly ILocalizationService _localizationService = Substitute.For<ILocalizationService>();

    [Fact]
    public void Format_ShouldFormatCorrectly()
    {
        // Arrange
        var modifier = new FallingLevelsModifier
        {
            Value = 2,
            LevelsFallen = 2
        };
        _localizationService.GetString("Modifier_FallingLevels").Returns("Falling Levels");
        _localizationService.GetString("Levels").Returns("Levels");

        // Act
        var result = modifier.Format(_localizationService);

        // Assert
        result.ShouldBe("2 (Falling Levels 2 Levels)");
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
