using Sanet.Localization;
using System.Globalization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;

/// <summary>
/// Modifier for a Piloting Skill Roll due to a leg being destroyed (specifically for pilot damage during fall).
/// </summary>
public record LegDestroyedModifier : RollModifier
{
    public override string Render(ILocalizationService localizationService)
    {
        return string.Format(CultureInfo.InvariantCulture, localizationService.GetString("Modifier_LegDestroyed"), Value);
    }
}
