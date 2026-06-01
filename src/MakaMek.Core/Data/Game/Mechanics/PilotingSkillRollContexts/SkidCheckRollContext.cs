using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

public record SkidCheckRollContext(int HexesMoved) : PilotingSkillRollContext(PilotingSkillRollType.SkidCheck)
{
    public override string Render(ILocalizationService localizationService)
        => string.Format(localizationService.GetString("PilotingSkillRollType_SkidCheck_WithHexes"),
            base.Render(localizationService), HexesMoved);
}
