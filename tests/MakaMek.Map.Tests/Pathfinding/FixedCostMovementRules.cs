using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Pathfinding;

namespace Sanet.MakaMek.Map.Tests.Pathfinding;

/// <summary>
/// Test double that charges the same entry cost for every transition, ignoring the terrain.
/// <see cref="TransitionCalls"/> lets a test prove whether a look-up was served from the cache.
/// </summary>
internal sealed class FixedCostMovementRules(int cost) : IHexMovementRules
{
    public int TransitionCalls { get; private set; }

    public IEnumerable<HexTransition> GetTransitions(
        Hex fromHex,
        Hex toHex,
        HexSurface fromSurface,
        int unitHeight,
        int? maxLevelChange)
    {
        TransitionCalls++;

        foreach (var surface in toHex.GetHexSurfaces())
        {
            yield return new HexTransition(
                surface,
                [new HexEnterMovementCost { Value = cost }],
                0);
        }
    }
}
