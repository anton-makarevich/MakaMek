using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;

/// <summary>
/// Per-type base modifier for a physical attack (Punch +0, Kick -2). Later milestones extend the
/// table: Push -1 (M2), Charge/DFA +0 (M3), physical weapons -1/-2 (M4), Club -1 (M5).
/// </summary>
public record PhysicalAttackBaseModifier : RollModifier
{
    public required PhysicalAttackType AttackType { get; init; }

    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_PhysicalAttackBase"), AttackType, Value);
}
