namespace Sanet.MakaMek.Core.Models.Units.Components.Weapons;

/// <summary>
/// Represents the range values for a weapon.
/// Each value is the maximum distance for that bracket.
/// </summary>
public record WeaponRange(
    int MinimumRange,
    int ShortRange,
    int MediumRange,
    int LongRange);
