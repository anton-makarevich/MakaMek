using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;

/// <summary>
/// Charge to-hit modifier from the difference between the attacker's and the target's Piloting
/// Skill Ratings (Total Warfare, p. 148). The value is the attacker's rating minus the target's:
/// a target with the lower (better) rating makes the charge harder, an attacker with the lower
/// rating makes it easier.
/// </summary>
public record RelativePilotingModifier : RollModifier
{
    /// <summary>The attacker's Piloting Skill Rating.</summary>
    public required int AttackerPiloting { get; init; }

    /// <summary>The target's Piloting Skill Rating.</summary>
    public required int TargetPiloting { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_RelativePiloting"),
            AttackerPiloting,
            TargetPiloting,
            Value);
}
