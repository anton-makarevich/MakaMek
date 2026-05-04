namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

/// <summary>
/// Represents the range values for a weapon.
/// Each value is the maximum distance for that bracket.
/// </summary>
public sealed record WeaponRange(
    int MinimumRange,
    int ShortRange,
    int MediumRange,
    int LongRange)
{
    /// <summary>
    /// Gets the range bracket for a given distance
    /// </summary>
    public RangeBracket GetRangeBracket(int distance)
    {
        if (distance == 0 && MinimumRange > 0) return RangeBracket.Minimum;
        if (distance <= ShortRange) return RangeBracket.Short;
        if (distance <= MediumRange) return RangeBracket.Medium;
        if (distance <= LongRange) return RangeBracket.Long;
        return RangeBracket.OutOfRange;
    }
}
