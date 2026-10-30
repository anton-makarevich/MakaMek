using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;

/// <summary>
/// Legalises a single forced entry into an adjacent hex for displaced units (Total Warfare,
/// p. 151-153). Unlike the movement-point based <see cref="IBattleMap.FindPath"/> rules, this
/// predicate only answers whether the unit can be placed in the hex and on which surface.
/// </summary>
public static class ForcedEntryCheck
{
    /// <summary>
    /// Checks whether <paramref name="unit"/> can be forced from <paramref name="from"/> into
    /// <paramref name="to"/>. When the destination has several surfaces (a bridge over ground),
    /// the ground surface is excluded when the unit does not fit under the bridge, and the
    /// remaining surface closest to the source standing level is selected.
    /// <para>
    /// A forced climb is limited by the unit's single-hex level-change limit (a 'Mech may be
    /// displaced up to two levels in one hex); a higher destination is prohibited terrain.
    /// Downward displacement is unlimited: drops of two or more levels are resolved as falls by
    /// the caller, not by this predicate.
    /// </para>
    /// </summary>
    public static ForcedEntryCheckResult Check(
        IUnit unit,
        IBattleMap map,
        HexCoordinates from,
        HexSurface fromSurface,
        HexCoordinates to)
    {
        if (!map.IsOnMap(to))
            return new ForcedEntryCheckResult(HexSurface.Ground, 0, DisplacementFailureReason.OffMap);

        if (map.GetHex(to) is not { } toHex)
            return new ForcedEntryCheckResult(HexSurface.Ground, 0, DisplacementFailureReason.ProhibitedTerrain);

        if (map.GetHex(from) is not { } fromHex)
            return new ForcedEntryCheckResult(HexSurface.Ground, 0, DisplacementFailureReason.OffMap);

        var maxAscent = Math.Max(0, unit.MaxLevelChangeForward);
        var candidates = new List<(HexSurface Surface, int Difference)>();
        var ascentTooHigh = false;

        foreach (var surface in toHex.GetHexSurfaces())
        {
            // A unit that cannot fit under the bridge cannot use the ground surface of a bridge hex.
            if (surface == HexSurface.Ground && !toHex.CanStandOnGround(unit.Height)) continue;

            var difference = toHex.GetElevationChange(fromHex, fromSurface, surface);
            if (difference > maxAscent)
            {
                ascentTooHigh = true;
                continue;
            }

            candidates.Add((surface, difference));
        }

        if (candidates.Count == 0)
            return new ForcedEntryCheckResult(HexSurface.Ground, 0,
                ascentTooHigh ? DisplacementFailureReason.TooHigh : DisplacementFailureReason.ProhibitedTerrain);

        var best = candidates
            .OrderBy(candidate => Math.Abs(candidate.Difference))
            .ThenBy(candidate => candidate.Surface == fromSurface ? 0 : 1)
            .First();

        return new ForcedEntryCheckResult(best.Surface, best.Difference, null);
    }
}
