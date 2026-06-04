using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

public record BridgeCollapseRollContext(int BridgeHeight) : PilotingSkillRollContext(PilotingSkillRollType.BridgeCollapse)
{
    public override string Render(ILocalizationService localizationService)
        => string.Format(localizationService.GetString("PilotingSkillRollType_BridgeCollapse_WithHeight"),
            base.Render(localizationService), BridgeHeight);
}
