using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;

/// <summary>
/// Everything the domino effect needs to calculate a displacement chain. The request carries no
/// mutable game state: the resolver only reads the map and the deployed units.
/// </summary>
public sealed record DominoEffectRequest
{
    /// <summary>The unit forced into <see cref="Destination"/>.</summary>
    public required IUnit EnteringUnit { get; init; }

    /// <summary>The hex the entering unit is forced into.</summary>
    public required HexCoordinates Destination { get; init; }

    /// <summary>The direction the chain continues, opposite the hexside the entering unit came through.</summary>
    public required HexDirection Direction { get; init; }

    public required DisplacementReason Reason { get; init; }

    public required Guid GameOriginId { get; init; }

    /// <summary>
    /// The units currently in <see cref="Destination"/>. When omitted, every deployed unit standing
    /// in the destination hex is considered.
    /// </summary>
    public IReadOnlyList<IUnit>? Occupants { get; init; }

    /// <summary>
    /// Surface to use for the entering unit and for the units initially in the destination hex
    /// instead of their own. Bridge collapse passes <see cref="HexSurface.Ground"/> because the deck
    /// is gone and every unit drops into the same ground hex before the chain is calculated.
    /// </summary>
    public HexSurface? OriginSurface { get; init; }

    /// <summary>
    /// When false, a deep drop into the occupied destination is not turned into an accidental fall
    /// from above (bridge collapse drops every unit together, so no unit falls onto another).
    /// </summary>
    public bool ApplyEntryElevationRule { get; init; } = true;
}
