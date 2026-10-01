using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// The attacker's Piloting Skill, which is the base to-hit for a physical attack where a weapon
/// attack would use Gunnery.
/// </summary>
public record PilotingRollModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_PilotingSkill"), Value);
}
