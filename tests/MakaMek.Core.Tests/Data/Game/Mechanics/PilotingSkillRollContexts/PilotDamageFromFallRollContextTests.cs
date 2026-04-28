using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Localization;
using Shouldly;
using Xunit;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics.PilotingSkillRollContexts;

public class PilotDamageFromFallRollContextTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();

    [Fact]
    public void Constructor_WhenCalled_SetsLevelsFallen()
    {
        // Arrange
        var levelsFallen = 2;

        // Act
        var sut = new PilotDamageFromFallRollContext(levelsFallen);

        // Assert
        sut.LevelsFallen.ShouldBe(levelsFallen);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsRollTypeToPilotDamageFromFall()
    {
        // Arrange
        var levelsFallen = 3;

        // Act
        var sut = new PilotDamageFromFallRollContext(levelsFallen);

        // Assert
        sut.RollType.ShouldBe(PilotingSkillRollType.PilotDamageFromFall);
    }

    [Fact]
    public void Render_WhenCalled_ReturnsLocalizedStringWithLevels()
    {
        // Arrange
        var sut = new PilotDamageFromFallRollContext(2);

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe("Pilot Damage From Fall (2 levels)");
    }

    [Theory]
    [InlineData(1, "Pilot Damage From Fall (1 levels)")]
    [InlineData(2, "Pilot Damage From Fall (2 levels)")]
    [InlineData(5, "Pilot Damage From Fall (5 levels)")]
    [InlineData(10, "Pilot Damage From Fall (10 levels)")]
    public void Render_WithDifferentLevels_ReturnsCorrectLocalizedString(int levelsFallen, string expected)
    {
        // Arrange
        var sut = new PilotDamageFromFallRollContext(levelsFallen);

        // Act
        var result = sut.Render(_localizationService);

        // Assert
        result.ShouldBe(expected);
    }
}
