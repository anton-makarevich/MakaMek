using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Pathfinding;

/// <summary>
/// BattleTech ground-movement rules for hex traversal: bridge clearance, road/pavement access,
/// terrain entry cost and elevation cost.
/// </summary>
internal sealed class HexMovementRules : IHexMovementRules
{
    public IEnumerable<HexTransition> GetTransitions(
        Hex fromHex,
        Hex toHex,
        HexSurface fromSurface,
        int unitHeight,
        int? maxLevelChange)
    {
        foreach (var toSurface in toHex.GetHexSurfaces())
        {
            if (!CanOccupySurface(fromHex, toHex, toSurface, unitHeight))
                continue;

            var levelCost = Math.Abs(toHex.GetElevationChange(fromHex, fromSurface, toSurface));

            if (maxLevelChange is { } max && levelCost > max)
                continue;

            var enterCost = toHex.GetEnterMovementCost(fromHex, fromSurface, toSurface).Sum(c => c.Value);
            yield return new HexTransition(toSurface, enterCost, levelCost);
        }
    }

    /// <summary>
    /// A unit can use the ground surface of a bridge hex only when it fits underneath the deck,
    /// and the bridge deck only when it either reaches it from road/pavement or cannot fit below.
    /// </summary>
    private static bool CanOccupySurface(Hex fromHex, Hex toHex, HexSurface toSurface, int unitHeight)
    {
        return toSurface switch
        {
            HexSurface.Ground => toHex.GetBridgeHeight() is null || toHex.CanStandOnGround(unitHeight),
            HexSurface.Bridge => fromHex.GetRoadOrPavedTerrain() is not null || !toHex.CanStandOnGround(unitHeight),
            _ => true
        };
    }
}
