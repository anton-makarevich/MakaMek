using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

/// <summary>
/// The +4 modifier the attacker takes on its piloting skill roll after a successful Death From
/// Above (Total Warfare, p. 150). The value comes from the rules provider.
/// </summary>
public record DfaAttackerImpactModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_DfaAttackerImpact"), Value);
}
