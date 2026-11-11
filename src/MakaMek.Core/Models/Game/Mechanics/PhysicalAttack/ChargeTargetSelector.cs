using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// The unit a charge resolves against and why it is that unit.
/// </summary>
/// <param name="Target">The unit in the committed destination hex, or null on an automatic failure.</param>
/// <param name="IsReplacementTarget">Whether the target replaced the originally declared one.</param>
/// <param name="IsAutoFail">Whether the charge automatically fails.</param>
public sealed record ChargeTargetSelection(
    IUnit? Target,
    bool IsReplacementTarget,
    bool IsAutoFail)
{
    public static ChargeTargetSelection AutoFail() => new(null, false, true);
}

/// <summary>
/// Selects the unit a charge resolves against at resolution time, because earlier attacks in the
/// physical attack phase can displace, destroy or fell the declared target (Total Warfare, p. 148).
/// <list type="bullet">
/// <item>The declared target is used if it is still a standing, non-destroyed unit in the
/// committed destination hex and on the committed surface.</item>
/// <item>Otherwise the first non-destroyed, non-prone enemy Mech in that hex is the replacement,
/// which is attacked at +1 to hit.</item>
/// <item>Otherwise the charge automatically fails: no to-hit roll, no damage and no charge PSRs.
/// The attacker never enters the destination hex; it is placed in a side hex of its forward arc
/// exactly like a rolled miss (Total Warfare, p. 148).</item>
/// </list>
/// A 'Mech performing a Death From Above is never a target, declared or replacement: its movement
/// is unfinished, so it cannot be the subject of a physical attack (Total Warfare, p. 149).
/// </summary>
public static class ChargeTargetSelector
{
    public static ChargeTargetSelection Select(
        IUnit attacker,
        Guid declaredTargetId,
        IEnumerable<IUnit> units)
    {
        var destination = attacker.MovementTaken?.Destination;
        if (destination is null) return ChargeTargetSelection.AutoFail();

        var deployed = units
            .Where(unit => unit is { IsDeployed: true, Position: not null })
            .ToList();

        var declaredTarget = deployed.FirstOrDefault(unit => unit.Id == declaredTargetId);
        if (declaredTarget is not null && IsValidTarget(declaredTarget, destination))
            return new ChargeTargetSelection(declaredTarget, false, false);

        var replacement = deployed.FirstOrDefault(unit =>
            unit.Id != declaredTargetId
            && !ReferenceEquals(unit, attacker)
            && IsValidTarget(unit, destination)
            && IsEnemy(unit, attacker));
        if (replacement is not null)
            return new ChargeTargetSelection(replacement, true, false);

        return ChargeTargetSelection.AutoFail();
    }

    /// <summary>
    /// The declared target is a legal charge target while it still stands on the committed
    /// destination hex and surface and is not performing a Death From Above, whose performer is
    /// immune to physical attacks until its movement is complete (Total Warfare, p. 149).
    /// </summary>
    private static bool IsValidTarget(IUnit unit, HexPosition destination) =>
        !unit.IsDestroyed
        && !unit.DeclaredPhysicalAttack.IsDfa()
        && unit is not Mech { IsProne: true }
        && IsInDestination(unit, destination);

    private static bool IsInDestination(IUnit unit, HexPosition destination) =>
        unit.Position is { } position
        && position.Coordinates == destination.Coordinates
        && position.Surface == destination.Surface;

    private static bool IsEnemy(IUnit unit, IUnit attacker) =>
        attacker.Owner is null || unit.Owner is null || unit.Owner != attacker.Owner;
}
