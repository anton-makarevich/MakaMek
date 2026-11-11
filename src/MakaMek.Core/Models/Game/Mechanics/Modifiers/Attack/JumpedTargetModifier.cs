using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// The cumulative +1 target modifier for a target that jumped (or is airborne as a pending DFA
/// attacker) this turn (Total Warfare, p. 117 and p. 148).
/// </summary>
public record JumpedTargetModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_JumpedTarget"), Value);
}
