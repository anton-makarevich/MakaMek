using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Localization;
using Shouldly;
using Xunit;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics.PilotingSkillRollContexts;

public class RubbleEntryRollContextTests
{
    private readonly ILocalizationService _localizationService = new FakeLocalizationService();

    [Fact]
    public void Constructor_WhenCalled_SetsRollTypeToRubbleEntry()
    {
        var sut = new RubbleEntryRollContext();

        sut.RollType.ShouldBe(PilotingSkillRollType.RubbleEntry);
    }

    [Fact]
    public void Render_WhenCalled_ReturnsLocalizedString()
    {
        var sut = new RubbleEntryRollContext();

        var result = sut.Render(_localizationService);

        result.ShouldBe("Rubble Entry");
    }
}
