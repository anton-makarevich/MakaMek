using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.Localization;
using Sanet.MakaMek.TestHelpers;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics.PilotingSkillRollContexts;

public class KickMissRollContextTests
{
    private readonly ILocalizationService _localizationService = TestLocalizationService.Create();

    [Fact]
    public void Constructor_WhenCalled_SetsRollTypeToKickMiss()
    {
        var sut = new KickMissRollContext();

        sut.RollType.ShouldBe(PilotingSkillRollType.KickMiss);
    }

    [Fact]
    public void Render_WhenCalled_ReturnsLocalizedString()
    {
        var sut = new KickMissRollContext();

        var result = sut.Render(_localizationService);

        result.ShouldBe("Missed Kick");
    }
}
