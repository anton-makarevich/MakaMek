namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Represents the reason why line of sight is blocked between two hexes.
/// </summary>
public enum LineOfSightBlockReason
{
    /// <summary>
    /// A hex's elevation level is at or above the interpolated LOS height at that position,
    /// physically blocking the line.
    /// </summary>
    Elevation,

    /// <summary>
    /// The accumulated intervening terrain factor along the path reached 3 or more,
    /// blocking the line of sight.
    /// </summary>
    InterveningTerrain,

    /// <summary>
    /// The source or target coordinates are off-map or the corresponding hex does not exist.
    /// </summary>
    InvalidCoordinates,

    /// <summary>
    /// Line of sight is blocked because one unit is submerged underwater while the other is on the surface.
    /// </summary>
    WaterSubmersion
}

