using Sanet.Localization;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;

/// <summary>
/// The per-type base modifier for a physical attack, on top of the attacker's Piloting Skill.
/// </summary>
public record PhysicalAttackBaseModifier : RollModifier
{
    public required PhysicalAttackType AttackType { get; init; }

    /// <summary>
    /// The base modifier for each attack type. Punch and kick are M1; the rest are here because
    /// they come from the same table in the rules and splitting them would mean editing this map
    /// again for every milestone.
    /// </summary>
    public static int BaseFor(PhysicalAttackType attackType) => attackType switch
    {
        PhysicalAttackType.Punch => 0,
        PhysicalAttackType.Kick => -2,
        PhysicalAttackType.Push => -1,
        PhysicalAttackType.Charge => 0,
        PhysicalAttackType.DFA => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(attackType), attackType,
            "No base to-hit modifier is defined for this physical attack type")
    };

    public static PhysicalAttackBaseModifier Create(PhysicalAttackType attackType) => new()
    {
        AttackType = attackType,
        Value = BaseFor(attackType)
    };

    public override string Render(ILocalizationService localizationService) =>
        string.Format(localizationService.GetString("Modifier_PhysicalAttackBase"), AttackType, Value);
}
