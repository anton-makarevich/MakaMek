using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;

/// <summary>
/// Base piloting skill modifier for a physical attack. Physical attacks target the attacker's
/// piloting skill rather than gunnery.
/// </summary>
public record PilotingRollModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_PilotingSkill"), Value);
}
