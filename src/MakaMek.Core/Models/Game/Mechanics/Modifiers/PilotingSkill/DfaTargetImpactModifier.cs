using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

/// <summary>
/// The +2 modifier the target of a successful Death From Above takes on its piloting skill roll
/// (Total Warfare, p. 150). The value comes from the rules provider.
/// </summary>
public record DfaTargetImpactModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_DfaTargetImpact"), Value);
}
