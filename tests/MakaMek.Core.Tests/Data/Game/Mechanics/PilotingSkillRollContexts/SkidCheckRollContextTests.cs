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
        var sut = new SkidCheckRollContext(skidDistance);

        sut.SkidDistance.ShouldBe(skidDistance);
    }

    [Fact]
    public void Constructor_WhenCalled_SetsRollTypeToSkidCheck()
    {
        var sut = new SkidCheckRollContext(3);

        sut.RollType.ShouldBe(PilotingSkillRollType.SkidCheck);
    }

    [Fact]
    public void Render_WhenCalled_ReturnsLocalizedStringWithHexes()
    {
        var sut = new SkidCheckRollContext(5);

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
        var sut = new SkidCheckRollContext(skidDistance);

        var result = sut.Render(_localizationService);

        result.ShouldBe(expected);
    }
}
