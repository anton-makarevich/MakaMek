using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Localization;
using Shouldly;
using Xunit;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics.PilotingSkillRollContexts;

public class SkidCheckRollContextTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();

    [Fact]
    public void Constructor_WhenCalled_SetsSkidDistance()
    {
        const int skidDistance = 5;
        var sut = new SkidCheckRollContext(skidDistance, 3);

        sut.SkidDistance.ShouldBe(skidDistance);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsHexesMoved()
    {
        const int hexesMoved = 7;
        var sut = new SkidCheckRollContext(5, hexesMoved);

        sut.HexesMoved.ShouldBe(hexesMoved);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsRollTypeToSkidCheck()
    {
        var sut = new SkidCheckRollContext(3, 5);

        sut.RollType.ShouldBe(PilotingSkillRollType.SkidCheck);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsAccidentalFallLevels()
    {
        const int fallLevels = 3;
        var sut = new SkidCheckRollContext(5, 3, fallLevels);

        sut.AccidentalFallLevels.ShouldBe(fallLevels);
    }

    [Fact]
    public void Constructor_WhenAccidentalFallLevelsNotProvided_SetsDefaultToZero()
    {
        var sut = new SkidCheckRollContext(5, 3);

        sut.AccidentalFallLevels.ShouldBe(0);
    }

    [Fact]
    public void Render_WhenCalled_ReturnsLocalizedStringWithHexes()
    {
        var sut = new SkidCheckRollContext(5, 3);

        var result = sut.Render(_localizationService);

        result.ShouldBe("Skid Check (5 hexes)");
    }

    [Theory]
    [InlineData(1, "Skid Check (1 hexes)")]
    [InlineData(2, "Skid Check (2 hexes)")]
    [InlineData(5, "Skid Check (5 hexes)")]
    [InlineData(10, "Skid Check (10 hexes)")]
    public void Render_WithDifferentSkidDistances_ReturnsCorrectLocalizedString(int skidDistance, string expected)
    {
        var sut = new SkidCheckRollContext(skidDistance, 3);

        var result = sut.Render(_localizationService);

        result.ShouldBe(expected);
    }

    [Fact]
    public void Render_WhenHasAccidentalFallLevels_IncludesCliffFallText()
    {
        var sut = new SkidCheckRollContext(5, 3, 2);

        var result = sut.Render(_localizationService);

        result.ShouldBe("Skid Check (5 hexes) + cliff fall (2 levels)");
    }

    [Theory]
    [InlineData(1, "Skid Check (5 hexes) + cliff fall (1 levels)")]
    [InlineData(3, "Skid Check (5 hexes) + cliff fall (3 levels)")]
    [InlineData(5, "Skid Check (5 hexes) + cliff fall (5 levels)")]
    public void Render_WithDifferentAccidentalFallLevels_ReturnsCorrectLocalizedString(int levels, string expected)
    {
        var sut = new SkidCheckRollContext(5, 3, levels);

        var result = sut.Render(_localizationService);

        result.ShouldBe(expected);
    }
}
