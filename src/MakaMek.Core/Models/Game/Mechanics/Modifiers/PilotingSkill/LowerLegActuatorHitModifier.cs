using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

/// <summary>
/// Modifier for a Piloting Skill Roll due to a critical hit on a Lower Leg Actuator.
/// </summary>
public record LowerLegActuatorHitModifier : RollModifier
{
    // The Value property is inherited from RollModifier and should be set to the specific modifier value (e.g., +1)
    // by the PilotingSkillCalculator based on game rules.
    
    public override string Render(ILocalizationService localizationService)
    {
        // Assuming a localization string like "Modifier_LowerLegActuatorHit" which might be:
        // "Lower Leg Actuator Hit: {0}" or simply "Lower Leg Actuator Hit" if the value is implied.
        // Using a format that includes the value for clarity, similar to DamagedGyroModifier.
        return string.Format(localizationService.GetString("Modifier_LowerLegActuatorHit"), Value);
    }
}
