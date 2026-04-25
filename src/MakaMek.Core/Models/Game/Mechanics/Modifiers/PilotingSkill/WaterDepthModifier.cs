using Sanet.MakaMek.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

/// <summary>
/// Represents a modifier to piloting skill rolls based on water depth.
/// Water depth affects the difficulty of piloting rolls when moving through water.
/// </summary>
public record WaterDepthModifier : RollModifier
{
    /// <summary>
    /// The water depth level (1, 2, or 3+)
    /// </summary>
    public required int WaterDepth { get; init; }

    /// <summary>
    /// Returns a string representation of this modifier.
    /// </summary>
    /// <returns>A string describing this modifier with depth context.</returns>
    public override string Render(ILocalizationService localizationService)
    {
        return string.Format(localizationService.GetString("Modifier_WaterDepth"), WaterDepth, Value);
    }
}
