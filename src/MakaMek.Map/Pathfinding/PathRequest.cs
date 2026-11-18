using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Map.Pathfinding;

/// <summary>
/// Immutable inputs for a single ground-movement path search.
/// </summary>
/// <param name="Mode">Shortest-cost or longest-distance search strategy.</param>
/// <param name="Start">Starting position, including facing and surface.</param>
/// <param name="Target">Destination position, including facing and surface.</param>
/// <param name="MovementType">Movement type recorded on the resulting path.</param>
/// <param name="MaxMovementPoints">Movement budget for the whole path.</param>
/// <param name="UnitHeight">Height of the moving unit, used for bridge clearance.</param>
/// <param name="ProhibitedHexes">Hexes that may not be entered, or null for none.</param>
/// <param name="MaxLevelChange">Maximum allowed elevation change per step, or null for no limit.</param>
internal sealed record PathRequest(
    PathFindingMode Mode,
    HexPosition Start,
    HexPosition Target,
    MovementType MovementType,
    int MaxMovementPoints,
    int UnitHeight,
    IReadOnlySet<HexCoordinates>? ProhibitedHexes,
    int? MaxLevelChange);
