using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.Localization;
using Sanet.MakaMek.TestHelpers;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Data.Game.Mechanics.PilotingSkillRollContexts;

public class KickHitRollContextTests
{
    private readonly ILocalizationService _localizationService = TestLocalizationService.Create();

    [Fact]
    public void Constructor_WhenCalled_SetsRollTypeToKickHit()
    {
        var sut = new KickHitRollContext();

        sut.RollType.ShouldBe(PilotingSkillRollType.KickHit);
    }

    [Fact]
    public void Render_WhenCalled_ReturnsLocalizedString()
    {
        var sut = new KickHitRollContext();

        var result = sut.Render(_localizationService);

        result.ShouldBe("Hit by Kick");
    }
}
