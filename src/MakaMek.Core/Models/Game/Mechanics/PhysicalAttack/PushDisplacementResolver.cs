using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Where a successful Push moves the target and the attacker. A null <see cref="PushDisplacementResolver.Resolve"/>
/// result means the push cannot displace anybody: the target is destroyed, no longer stands directly in
/// front of the attacker, or the destination is blocked.
/// </summary>
/// <param name="TargetDestination">The hex the pushed target moves to.</param>
/// <param name="AttackerDestination">The hex the attacker advances into.</param>
/// <param name="LevelsFallen">
/// How many standing levels the target drops. Zero for a level move and for an upward displacement.
/// </param>
/// <param name="TriggersAutomaticFall">
/// Whether the drop exceeds the deepest displacement drop that does not force a fall, as
/// determined by the rules provider (Total Warfare, p. 148, 151).
/// </param>
/// <param name="DominoEffect">
/// The resolved domino chain when the destination was occupied. Its commands must be applied
/// before the target moves into the vacated hex and the attacker advances. Null when the
/// destination was free.
/// </param>
public record PushDisplacementResult(
    HexPosition TargetDestination,
    HexPosition AttackerDestination,
    int LevelsFallen,
    bool TriggersAutomaticFall,
    DominoEffectResult? DominoEffect = null);

/// <summary>
/// Applies the explicit Push destination policy from docs/rules/physical-attacks.md section 4.
/// The displaced target moves one hex directly away from the attacker and the attacker advances
/// into the vacated hex. Both legs use the shared <see cref="ForcedEntryCheck"/> policy: an ascent
/// is allowed up to the displaced unit's single-hex level-change limit (two levels for a 'Mech),
/// and a destination above that limit is prohibited terrain. Downward displacement is unrestricted; a drop
/// of one or two levels is a normal push and a deeper drop ends in an automatic fall.
/// <para>
/// The attacker advance must also be legal, and the attacker must not drop more than the
/// automatic-fall threshold. When either condition fails the whole push is blocked, so neither
/// unit moves, by analogy with Total Warfare p. 151's "neither unit moves" rule.
/// </para>
/// <para>
/// When the destination is occupied, the target triggers the shared domino effect: the occupant is
/// dislodged in the push direction and the chain continues from there. A first link that cannot be
/// resolved (blocked entry, accidental fall from above) moves nobody, keeping the push's own PSR; a
/// later blocked link keeps the displacements already resolved (the blocked unit stays put and
/// shares its hex, so both roll).
/// </para>
/// <para>
/// The destination is evaluated from the positions at application time, not at declaration time:
/// an earlier push in the same phase can have moved either unit. A 'Mech that is airborne for a
/// pending Death From Above is not a ground occupant, so it does not trigger a chain and cannot be
/// displaced while it is in the air (Total Warfare, p. 149).
/// </para>
/// </summary>
public class PushDisplacementResolver(
    IRulesProvider rulesProvider,
    DominoEffectResolver dominoEffectResolver)
{
    /// <summary>
    /// The displacement of <paramref name="target"/>, or null when it cannot be displaced.
    /// </summary>
    /// <param name="attacker">The Mech that made the push.</param>
    /// <param name="target">The pushed unit.</param>
    /// <param name="map">The battle map.</param>
    /// <param name="units">Every unit in the game, to detect an occupied destination.</param>
    /// <param name="gameOriginId">Used for the displacement commands of a resolved domino chain.</param>
    public PushDisplacementResult? Resolve(
        IUnit attacker,
        IUnit target,
        IBattleMap map,
        IEnumerable<IUnit> units,
        Guid gameOriginId = default)
    {
        if (attacker.IsDestroyed || target.IsDestroyed) return null;
        if (attacker.Position is not { } attackerPosition || target.Position is not { } targetPosition) return null;

        var pushDirection = attackerPosition.Facing;
        var destinationCoordinates = targetPosition.Coordinates.GetNeighbour(pushDirection);

        if (!IsInFrontOfTheAttacker(attackerPosition, targetPosition, pushDirection)) return null;
        if (!map.IsOnMap(destinationCoordinates)) return null;
        if (map.GetHex(destinationCoordinates) is not { } destinationHex) return null;
        if (map.GetHex(targetPosition.Coordinates) is not { } targetHex) return null;

        var allUnits = units.ToList();
        var occupants = allUnits
            .Where(unit => !ReferenceEquals(unit, target)
                           && unit.IsDeployed
                           && !unit.IsAirborneDfaAttacker
                           && unit.Position!.Coordinates == destinationCoordinates)
            .ToList();

        var targetStandingLevel = targetHex.GetStandingLevel(targetPosition.Surface);

        if (occupants.Count != 0)
            return ResolveDomino(
                attacker, target, targetPosition, attackerPosition, pushDirection,
                destinationCoordinates, targetStandingLevel, occupants, allUnits, map, gameOriginId);

        // The empty destination uses the same forced-entry policy as the domino path: it selects
        // the surface (ground or bridge deck) and enforces the unit's single-hex climb limit.
        var entry = ForcedEntryCheck.Check(
            target, map, targetPosition.Coordinates, targetPosition.Surface, destinationCoordinates);
        if (!entry.IsLegal) return null;

        var attackerAdvance = CheckAttackerAdvance(attacker, attackerPosition, targetPosition.Coordinates, map);
        if (attackerAdvance is null) return null;

        var destinationStandingLevel = destinationHex.GetStandingLevel(entry.Surface);
        var levelsFallen = Math.Max(0, targetStandingLevel - destinationStandingLevel);
        return new PushDisplacementResult(
            new HexPosition(destinationCoordinates, targetPosition.Facing, entry.Surface),
            new HexPosition(targetPosition.Coordinates, attackerPosition.Facing, attackerAdvance.Surface),
            levelsFallen,
            levelsFallen > rulesProvider.GetMaxDisplacementDropWithoutAutomaticFall());
    }

    private PushDisplacementResult? ResolveDomino(
        IUnit attacker,
        IUnit target,
        HexPosition targetPosition,
        HexPosition attackerPosition,
        HexDirection pushDirection,
        HexCoordinates destinationCoordinates,
        int targetStandingLevel,
        IReadOnlyList<IUnit> occupants,
        IReadOnlyList<IUnit> allUnits,
        IBattleMap map,
        Guid gameOriginId)
    {
        var domino = dominoEffectResolver.Resolve(
            new DominoEffectRequest
            {
                EnteringUnit = target,
                Destination = destinationCoordinates,
                Direction = pushDirection,
                Reason = DisplacementReason.DominoEffect,
                GameOriginId = gameOriginId,
                Occupants = occupants,
                ApplyEntryElevationRule = true
            },
            map,
            allUnits);

        // A blocked first link or an accidental fall from above cancels the whole chain: neither the
        // target nor the attacker moves, but the push PSR still happens. A blocked later link keeps
        // the displacements already resolved, and the target still enters the vacated hex.
        if (!domino.HasDisplacements) return null;

        // The attacker must still be able to take the vacated hex. If it cannot, the resolved chain
        // is discarded and the whole push is blocked, so no domino command is applied.
        var attackerAdvance = CheckAttackerAdvance(attacker, attackerPosition, targetPosition.Coordinates, map);
        if (attackerAdvance is null) return null;

        var destinationStandingLevel = map.GetHex(destinationCoordinates)!.GetStandingLevel(domino.EnteringSurface);
        var levelsFallen = Math.Max(0, targetStandingLevel - destinationStandingLevel);

        return new PushDisplacementResult(
            new HexPosition(destinationCoordinates, targetPosition.Facing, domino.EnteringSurface),
            new HexPosition(targetPosition.Coordinates, attackerPosition.Facing, attackerAdvance.Surface),
            levelsFallen,
            levelsFallen > rulesProvider.GetMaxDisplacementDropWithoutAutomaticFall(),
            domino);
    }

    /// <summary>
    /// The forced entry for the attacker into the target's vacated hex, or null when the advance is
    /// illegal or drops the attacker more than the automatic-fall threshold.
    /// </summary>
    private ForcedEntryCheckResult? CheckAttackerAdvance(
        IUnit attacker,
        HexPosition attackerPosition,
        HexCoordinates vacatedCoordinates,
        IBattleMap map)
    {
        var entry = ForcedEntryCheck.Check(
            attacker, map, attackerPosition.Coordinates, attackerPosition.Surface, vacatedCoordinates);
        if (!entry.IsLegal) return null;

        var attackerStandingLevel = map.GetHex(attackerPosition.Coordinates)!
            .GetStandingLevel(attackerPosition.Surface);
        var destinationStandingLevel = map.GetHex(vacatedCoordinates)!.GetStandingLevel(entry.Surface);
        var drop = attackerStandingLevel - destinationStandingLevel;

        return drop > rulesProvider.GetMaxDisplacementDropWithoutAutomaticFall() ? null : entry;
    }

    private static bool IsInFrontOfTheAttacker(
        HexPosition attackerPosition,
        HexPosition targetPosition,
        HexDirection pushDirection) =>
        targetPosition.Coordinates == attackerPosition.Coordinates.GetNeighbour(pushDirection);
}
