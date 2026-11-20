using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Map.Models;
using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

public record CliffFallRollContext(int LevelsFallen, DiceResult FacingDiceRoll, HexDirection FacingAfterFall)
    : PilotingSkillRollContext(PilotingSkillRollType.CliffFall)
{
    public override string Render(ILocalizationService localizationService)
        => string.Format(CultureInfo.InvariantCulture, localizationService.GetString("PilotingSkillRollType_CliffFall_WithLevels"),
            base.Render(localizationService), LevelsFallen);
}
