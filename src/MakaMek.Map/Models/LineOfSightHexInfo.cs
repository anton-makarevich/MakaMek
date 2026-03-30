namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Captures per-hex line-of-sight data computed during a <see cref="IBattleMap.GetLineOfSight"/> traversal.
/// Wraps the existing <see cref="Hex"/> with the two context-dependent computed values that cannot be
/// derived from the hex alone.
/// </summary>
public record LineOfSightHexInfo
{
    /// <summary>
    /// The hex at this position along the intervening LOS path.
    /// All standard hex data (coordinates, level, ceiling, terrains) is accessible via this reference.
    /// </summary>
    public required Hex Hex { get; init; }

    /// <summary>
    /// The interpolated LOS height at this hex's position along the line, computed from
    /// attacker height + attacker level linearly to target height + target level.
    /// </summary>
    public required double InterpolatedLosHeight { get; init; }

    /// <summary>
    /// The intervening terrain factor this hex contributes to the running total.
    /// This is 0 when the hex ceiling does not reach <see cref="InterpolatedLosHeight"/>
    /// (i.e. the terrain is below the LOS line and does not intervene).
    /// </summary>
    public required int InterveningFactor { get; init; }
}

