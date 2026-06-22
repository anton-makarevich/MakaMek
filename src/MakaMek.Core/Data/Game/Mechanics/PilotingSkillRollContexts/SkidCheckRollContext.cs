using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

public record SkidCheckRollContext(int SkidDistance, int HexesMoved, int AccidentalFallLevels = 0) : PilotingSkillRollContext(PilotingSkillRollType.SkidCheck)
{
    public override string Render(ILocalizationService localizationService)
    {
        var baseString = string.Format(localizationService.GetString("PilotingSkillRollType_SkidCheck_WithHexes"),
            base.Render(localizationService), SkidDistance);

        if (AccidentalFallLevels > 0)
        {
            baseString += string.Format(
                localizationService.GetString("PilotingSkillRollType_SkidCheck_CliffFall"),
                AccidentalFallLevels);
        }

        return baseString;
    }
}
