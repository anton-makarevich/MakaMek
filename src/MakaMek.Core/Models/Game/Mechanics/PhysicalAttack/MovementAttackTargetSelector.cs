using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Selects the unit a Charge or DFA resolves against at resolution time, because earlier attacks in
/// the physical attack phase can displace, destroy or fell the declared target (Total Warfare,
/// p. 148-150). This is the single target-selection rule for both movement-origin attacks: the
/// declaration validator and the resolution phase both use it, so they cannot drift.
/// <list type="bullet">
/// <item>The declared target is used while it still stands in the hex the move committed to. A
/// Charge also needs it not destroyed and not prone; a DFA may land on a destroyed or prone unit
/// (p. 150-151).</item>
/// <item>Otherwise the first valid enemy in that hex is the replacement, which is attacked at +1 to
/// hit. A Charge can replace with any unit; a DFA only with a Mech.</item>
/// <item>Otherwise the attack automatically fails.</item>
/// </list>
/// An airborne 'Mech performing a Death From Above is never a target, declared or replacement: its
/// movement is unfinished, so it cannot be the subject of a physical attack (Total Warfare, p. 149).
/// Once it has landed its immunity has ended and it is an ordinary occupant of the hex.
/// </summary>
public static class MovementAttackTargetSelector
{
    public static MovementAttackTargetSelection Select(
        IUnit attacker,
        Guid declaredTargetId,
        PhysicalAttackType attackType,
        IEnumerable<IUnit> units)
    {
        var destination = attacker.MovementTaken?.Destination;
        if (destination is null) return MovementAttackTargetSelection.AutoFail();

        var deployed = units
            .Where(unit => unit is { IsDeployed: true, Position: not null })
            .ToList();

        var declaredTarget = deployed.FirstOrDefault(unit => unit.Id == declaredTargetId);
        if (declaredTarget is not null && IsEligibleDeclaredTarget(declaredTarget, destination, attackType))
            return new MovementAttackTargetSelection(declaredTarget, false, false);

        var replacement = deployed.FirstOrDefault(unit =>
            unit.Id != declaredTargetId
            && !ReferenceEquals(unit, attacker)
            && IsEligibleReplacement(unit, destination, attackType, attacker));

        return replacement is not null
            ? new MovementAttackTargetSelection(replacement, true, false)
            : MovementAttackTargetSelection.AutoFail();
    }

    /// <summary>
    /// The declared target stays the target while it occupies the committed hex. A Charge needs a
    /// standing, non-destroyed unit; a DFA resolves against a prone or even a destroyed occupant,
    /// which turns the attack into a miss without a roll instead of an auto-fail.
    /// </summary>
    private static bool IsEligibleDeclaredTarget(IUnit unit, HexPosition destination,
        PhysicalAttackType attackType) =>
        attackType switch
        {
            PhysicalAttackType.Charge =>
                unit is { IsDestroyed: false, IsProne: false, IsAirborneDfaAttacker: false }
                && IsInDestination(unit, destination),
            PhysicalAttackType.DFA =>
                !unit.IsAirborneDfaAttacker && IsInDestination(unit, destination),
            _ => false
        };

    /// <summary>
    /// A replacement has to be a living enemy in the committed hex. A Charge replaces with any unit
    /// type, a DFA only with a Mech.
    /// </summary>
    private static bool IsEligibleReplacement(IUnit unit, HexPosition destination,
        PhysicalAttackType attackType, IUnit attacker) =>
        unit is { IsDestroyed: false, IsAirborneDfaAttacker: false }
        && IsInDestination(unit, destination)
        && attackType switch
        {
            PhysicalAttackType.Charge => !unit.IsProne && IsEnemy(unit, attacker),
            PhysicalAttackType.DFA => unit is Mech && IsEnemy(unit, attacker),
            _ => false
        };

    private static bool IsInDestination(IUnit unit, HexPosition destination) =>
        unit.Position is { } position
        && position.Coordinates == destination.Coordinates
        && position.Surface == destination.Surface;

    private static bool IsEnemy(IUnit unit, IUnit attacker) =>
        attacker.Owner is null || unit.Owner is null || unit.Owner != attacker.Owner;
}
