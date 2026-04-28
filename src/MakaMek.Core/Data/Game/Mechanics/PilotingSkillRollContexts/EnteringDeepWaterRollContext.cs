using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;

/// <summary>
/// Piloting skill roll context for entering deep water
/// </summary>
public record EnteringDeepWaterRollContext(int WaterDepth) : PilotingSkillRollContext(PilotingSkillRollType.WaterEntry)
{
    /// <inheritdoc />
    public override string Render(ILocalizationService localizationService)
        => string.Format(localizationService.GetString("PilotingSkillRollType_WaterEntry_WithDepth"),
            base.Render(localizationService), WaterDepth);
}
