using Sanet.MakaMek.Core.Services.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

/// <summary>
/// Represents a modifier to a piloting skill roll due to a 'Mech taking 20 or more damage in a single phase.
/// </summary>
public record HeavyDamageModifier : RollModifier
{
    /// <summary>
    /// The total damage taken by the 'Mech in this phase.
    /// </summary>
    public int DamageTaken { get; init; }

    /// <summary>
    /// Returns a string representation of this modifier.
    /// </summary>
    /// <returns>A string describing this modifier.</returns>
    public override string Render(ILocalizationService localizationService)
    {
        return string.Format(localizationService.GetString("Modifier_HeavyDamage"), DamageTaken, Value);
    }
}
