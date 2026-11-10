using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

/// <summary>
/// The +2 penalty both units take on the piloting skill roll after a successful charge
/// (Total Warfare, p. 148). The value comes from the rules provider.
/// </summary>
public record ChargeImpactModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_ChargeImpact"), Value);
}
