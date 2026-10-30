using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Displacement;

/// <summary>
/// The outcome of <see cref="ForcedEntryCheck"/>: the surface the unit would occupy and the level
/// difference from the source hex, or the reason the entry is impossible.
/// </summary>
public sealed record ForcedEntryCheckResult(
    HexSurface Surface,
    int LevelDifference,
    DisplacementFailureReason? Failure)
{
    public bool IsLegal => Failure is null;

    /// <summary>
    /// True when the destination is two or more levels below the source (an accidental fall from
    /// above instead of a domino when the destination is occupied).
    /// </summary>
    public bool IsDeepDrop => LevelDifference <= -2;
}