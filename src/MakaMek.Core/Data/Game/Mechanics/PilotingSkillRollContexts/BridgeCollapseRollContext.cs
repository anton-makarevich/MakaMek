using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

public record BridgeCollapseRollContext(int BridgeHeight) : PilotingSkillRollContext(PilotingSkillRollType.BridgeCollapse)
{
    public override string Render(ILocalizationService localizationService)
        => string.Format(CultureInfo.InvariantCulture, localizationService.GetString("PilotingSkillRollType_BridgeCollapse_WithHeight"),
            base.Render(localizationService), BridgeHeight);
}
