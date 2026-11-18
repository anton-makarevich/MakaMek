using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.MovementCosts;

namespace Sanet.MakaMek.Map.Pathfinding;

/// <summary>
/// A single legal transition from a hex to one of the surfaces of an adjacent hex.
/// </summary>
/// <param name="Surface">Destination surface that can be occupied.</param>
/// <param name="EnterCosts">
/// Terrain and entry costs of entering the destination hex, as priced by the movement rules.
/// The accepted cost breakdown is carried into the returned path so the search and the segments
/// always agree.
/// </param>
/// <param name="LevelCost">Extra cost for the elevation difference between the two surfaces.</param>
internal readonly record struct HexTransition(
    HexSurface Surface,
    IReadOnlyList<MovementCost> EnterCosts,
    int LevelCost)
{
    /// <summary>
    /// Total movement cost of the transition: the entry cost breakdown plus the elevation cost.
    /// </summary>
    public int MovementCost => EnterCosts.Sum(cost => cost.Value) + LevelCost;

    /// <summary>
    /// Returns a transition that owns an independent copy of its entry costs. The search calls
    /// this before retaining an accepted step, because a movement-rules implementation may reuse
    /// or mutate its backing collection for the next transition it yields.
    /// </summary>
    public HexTransition WithOwnedCosts()
    {
        return this with { EnterCosts = [.. EnterCosts] };
    }
}
