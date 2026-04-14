namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Result of canonicalizing a 6-bit hex-neighbor bitmask.
/// The canonical form is the lowest numeric value obtainable by rotating the mask
/// across all 6 possible 60° orientations.
/// Rotation direction is clockwise, matching <see cref="HexDirection"/> ordering
/// (Top=0, TopRight=1, … TopLeft=5).
/// </summary>
/// <param name="CanonicalMask">The lowest-value 6-bit rotation of the original bitmask.</param>
/// <param name="RotationSteps">
/// Number of 60° clockwise rotations applied to reach the canonical form (0–5).
/// </param>
public record CanonicalBitmaskResult(byte CanonicalMask, int RotationSteps);
