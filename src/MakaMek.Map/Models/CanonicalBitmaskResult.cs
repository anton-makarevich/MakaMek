namespace Sanet.MakaMek.Map.Models;

/// <summary>
/// Result of canonicalizing a 6-bit hex-neighbor bitmask.
/// The canonical form is the lowest numeric value obtainable by rotating the mask
/// across all 6 possible 60° orientations.
/// Rotation direction is clockwise, matching <see cref="HexDirection"/> ordering
/// (Top=0, TopRight=1, … TopLeft=5).
/// </summary>
public sealed record CanonicalBitmaskResult
{
    public byte CanonicalMask { get; }
    public int RotationSteps { get; }

    /// <param name="canonicalMask">The lowest-value 6-bit rotation of the original bitmask.</param>
    /// <param name="rotationSteps">
    /// Number of 60° clockwise rotations applied to reach the canonical form (0–5).
    /// </param>
    public CanonicalBitmaskResult(byte canonicalMask, int rotationSteps)
    {
        if ((canonicalMask & 0b1100_0000) != 0)
            throw new ArgumentOutOfRangeException(nameof(canonicalMask),
                "Canonical mask must be a 6-bit value (0-63).");
        if (rotationSteps is < 0 or > 5)
            throw new ArgumentOutOfRangeException(nameof(rotationSteps),
                "Rotation steps must be in range 0..5.");

        CanonicalMask = canonicalMask;
        RotationSteps = rotationSteps;
    }
}
