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

    /// <summary>
    /// The reason stamped on the domino links: each is caused by the preceding displacement into an
    /// occupied hex, so it is a domino effect regardless of what started the chain (Total Warfare
    /// p. 57, p. 152). See <see cref="FirstLinkDisplacementReason"/> for a first displacement that is
    /// a direct consequence of the action rather than a domino link.
    /// </summary>
    public required DisplacementReason Reason { get; init; }

    /// <summary>
    /// The reason for the first displacement this request calculates — the unit forced out of
    /// <see cref="Destination"/> — when the action itself causes it directly, as a Death From Above
    /// landing knocks the occupant out. Every later link keeps <see cref="Reason"/>. When omitted,
    /// the first link is a domino link too and uses <see cref="Reason"/>.
    /// </summary>
    public DisplacementReason? FirstLinkDisplacementReason { get; init; }

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

    /// <summary>
    /// True when the action that moves the entering unit already guarantees it may occupy
    /// <see cref="Destination"/>: a 'Mech completing a Death From Above jump, for instance. Its
    /// entry was validated when the action was declared (jump MP and clearance), so the forced
    /// climb limit and the accidental-fall-from-above rule are not applied to it. The occupant
    /// chain is still checked; only the entering unit's own entry is exempt.
    /// </summary>
    public bool EnteringEntryIsGuaranteed { get; init; }

    /// <summary>
    /// The surface the entering unit occupies at <see cref="Destination"/>. Only used when
    /// <see cref="EnteringEntryIsGuaranteed"/> is true, because the forced-entry check that normally
    /// selects the surface is skipped.
    /// </summary>
    public HexSurface DestinationSurface { get; init; } = HexSurface.Ground;
}
