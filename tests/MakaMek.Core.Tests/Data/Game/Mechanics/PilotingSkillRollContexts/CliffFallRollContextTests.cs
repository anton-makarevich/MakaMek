using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics.PilotingSkillRollContexts;

public class CliffFallRollContextTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();

    [Fact]
    public void Constructor_WhenCalled_SetsLevelsFallen()
    {
        const int levelsFallen = 3;
        var sut = new CliffFallRollContext(levelsFallen, new DiceResult(3), HexDirection.BottomLeft);

        sut.LevelsFallen.ShouldBe(levelsFallen);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsFacingDiceRoll()
    {
        var facingDiceRoll = new DiceResult(5);
        var sut = new CliffFallRollContext(2, facingDiceRoll, HexDirection.TopRight);

        sut.FacingDiceRoll.ShouldBe(facingDiceRoll);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsFacingAfterFall()
    {
        const HexDirection expected = HexDirection.BottomRight;
        var sut = new CliffFallRollContext(2, new DiceResult(4), expected);

        sut.FacingAfterFall.ShouldBe(expected);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsRollTypeToCliffFall()
    {
        var sut = new CliffFallRollContext(2, new DiceResult(3), HexDirection.TopLeft);

        sut.RollType.ShouldBe(PilotingSkillRollType.CliffFall);
    }

    [Fact]
    public void Render_WhenCalled_ReturnsLocalizedStringWithLevels()
    {
        var sut = new CliffFallRollContext(3, new DiceResult(3), HexDirection.BottomLeft);

        var result = sut.Render(_localizationService);

        result.ShouldBe("Cliff Fall (3 levels)");
    }

    [Theory]
    [InlineData(1, "Cliff Fall (1 levels)")]
    [InlineData(2, "Cliff Fall (2 levels)")]
    [InlineData(5, "Cliff Fall (5 levels)")]
    [InlineData(10, "Cliff Fall (10 levels)")]
    public void Render_WithDifferentLevels_ReturnsCorrectLocalizedString(int levelsFallen, string expected)
    {
        var sut = new CliffFallRollContext(levelsFallen, new DiceResult(3), HexDirection.BottomLeft);

        var result = sut.Render(_localizationService);

        result.ShouldBe(expected);
    }
}
