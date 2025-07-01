using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

/// <summary>
/// Modifier for a Piloting Skill Roll due to a critical hit on a Hip Actuator.
/// </summary>
public record HipActuatorHitModifier : RollModifier
{
    // The Value property is inherited from RollModifier and should be set to the specific modifier value (e.g., +2)
    // by the PilotingSkillCalculator based on game rules.
    
    public override string Render(ILocalizationService localizationService)
    {
        return string.Format(localizationService.GetString("Modifier_HipActuatorHit"), Value);
    }
}
