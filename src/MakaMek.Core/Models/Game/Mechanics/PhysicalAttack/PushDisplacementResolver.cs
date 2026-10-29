using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Where a successful Push moves the target and the attacker. A null <see cref="Resolve"/> result
/// means the push cannot displace anybody: the target is destroyed, no longer stands directly in
/// front of the attacker, or the destination is blocked.
/// </summary>
/// <param name="TargetDestination">The hex the pushed target moves to.</param>
/// <param name="AttackerDestination">The hex the attacker advances into.</param>
/// <param name="LevelsFallen">How many standing levels the target drops.</param>
/// <param name="TriggersAutomaticFall">
/// Whether the drop exceeds the deepest displacement drop that does not force a fall, as
/// determined by the rules provider (Total Warfare, p. 148, 151).
/// </param>
public record PushDisplacementResult(
    HexPosition TargetDestination,
    HexPosition AttackerDestination,
    int LevelsFallen,
    bool TriggersAutomaticFall);

/// <summary>
/// Applies the explicit Push destination policy from docs/rules/physical-attacks.md section 4.
/// The displaced target moves one hex directly away from the attacker and the attacker advances
/// into the vacated hex. The destination is blocked when it is off the map, occupied by another
/// unit, or stands higher than the target's current standing level. A drop of one or two levels
/// is a normal push; a deeper drop is allowed and ends in an automatic fall.
/// <para>
/// The destination is evaluated from the positions at application time, not at declaration time:
/// an earlier push in the same phase can have moved either unit. Ordinary movement and
/// bridge-domino rules are deliberately not reused.
/// </para>
/// </summary>
public class PushDisplacementResolver(IRulesProvider rulesProvider)
{
    /// <summary>
    /// The displacement of <paramref name="target"/>, or null when it cannot be displaced.
    /// </summary>
    /// <param name="attacker">The Mech that made the push.</param>
    /// <param name="target">The pushed unit.</param>
    /// <param name="map">The battle map.</param>
    /// <param name="units">Every unit in the game, to detect an occupied destination.</param>
    public PushDisplacementResult? Resolve(
        IUnit attacker,
        IUnit target,
        IBattleMap map,
        IEnumerable<IUnit> units)
    {
        if (attacker.IsDestroyed || target.IsDestroyed) return null;
        if (attacker.Position is not { } attackerPosition || target.Position is not { } targetPosition) return null;

        var pushDirection = attackerPosition.Facing;
        var destinationCoordinates = targetPosition.Coordinates.GetNeighbour(pushDirection);

        if (!IsInFrontOfTheAttacker(attackerPosition, targetPosition, pushDirection)) return null;
        if (!map.IsOnMap(destinationCoordinates)) return null;
        if (map.GetHex(destinationCoordinates) is not { } destinationHex) return null;

        if (units.Any(unit =>
                !ReferenceEquals(unit, target)
                && unit.IsDeployed
                && unit.Position!.Coordinates == destinationCoordinates))
            return null;

        if (map.GetHex(targetPosition.Coordinates) is not { } targetHex) return null;
        var targetStandingLevel = targetHex.GetStandingLevel(targetPosition.Surface);

        // Stay on the bridge only when its deck is exactly where the target already stands;
        // otherwise the target lands on the ground under (or above) the deck.
        var destinationSurface = destinationHex.GetBridgeHeight() != null
                                 && destinationHex.GetStandingLevel(HexSurface.Bridge) == targetStandingLevel
            ? HexSurface.Bridge
            : HexSurface.Ground;
        var destinationStandingLevel = destinationHex.GetStandingLevel(destinationSurface);

        // The repo rules treat any higher destination as blocked, even though Total Warfare
        // allows a small upward step.
        if (destinationStandingLevel > targetStandingLevel) return null;

        var levelsFallen = targetStandingLevel - destinationStandingLevel;
        return new PushDisplacementResult(
            new HexPosition(destinationCoordinates, targetPosition.Facing, destinationSurface),
            new HexPosition(targetPosition.Coordinates, attackerPosition.Facing, targetPosition.Surface),
            levelsFallen,
            levelsFallen > rulesProvider.GetMaxDisplacementDropWithoutAutomaticFall());
    }

    private static bool IsInFrontOfTheAttacker(
        HexPosition attackerPosition,
        HexPosition targetPosition,
        HexDirection pushDirection) =>
        targetPosition.Coordinates == attackerPosition.Coordinates.GetNeighbour(pushDirection);
}
