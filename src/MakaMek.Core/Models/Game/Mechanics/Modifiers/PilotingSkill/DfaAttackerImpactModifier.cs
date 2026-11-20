using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

/// <summary>
/// The +4 modifier the attacker takes on its piloting skill roll after a successful Death From
/// Above (Total Warfare, p. 150). The value comes from the rules provider.
/// </summary>
public record DfaAttackerImpactModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(CultureInfo.InvariantCulture, localizationService.GetString("Modifier_DfaAttackerImpact"), Value);
}
