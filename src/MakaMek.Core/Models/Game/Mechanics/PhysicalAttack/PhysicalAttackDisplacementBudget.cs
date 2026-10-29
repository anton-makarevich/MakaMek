using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// The shared one-displacement-per-target-per-turn limit (Total Warfare, p. 148-149): a unit may
/// only be the target of one Charge, DFA or Push attack in a given turn.
/// <para>
/// The budget is derived from the turn-scoped <see cref="IUnit.DeclaredPhysicalAttack"/> values,
/// so it needs no extra state and clears with the declarations at turn end. A rejected command
/// never stores a declaration, so failed validation cannot reserve a target.
/// </para>
/// </summary>
public static class PhysicalAttackDisplacementBudget
{
    /// <summary>
    /// Whether any unit has declared a displacement attack (Push, Charge or DFA) against the target.
    /// </summary>
    public static bool IsReserved(Guid targetId, IEnumerable<IUnit> units) =>
        units.Any(unit =>
            unit.DeclaredPhysicalAttack is { } declaration
            && declaration.TargetId == targetId
            && PhysicalAttackDefinitions.IsDisplacementAttack(declaration.AttackType));
}
