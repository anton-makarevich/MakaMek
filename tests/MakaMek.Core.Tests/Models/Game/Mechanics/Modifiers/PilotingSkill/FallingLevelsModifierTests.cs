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
        var sut = new FallingLevelsModifier
        {
            Value = 2,
            LevelsFallen = 2
        };
        _localizationService.GetString("Modifier_FallingLevels").Returns("Falling ({0} {1}): +{2}");
        _localizationService.GetString("Levels").Returns("Levels");

        // Act
        var result = sut.Render(_localizationService);

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
        var sut = new FallingLevelsModifier
        {
            Value = expectedValue,
            LevelsFallen = levelsFallen
        };

        // Assert
        sut.Value.ShouldBe(expectedValue);
        sut.LevelsFallen.ShouldBe(levelsFallen);
    }
}
