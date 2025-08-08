using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Serializable data to identify a specific weapon on a unit
/// </summary>
public record WeaponData
{
    public required string Name { get; init; }
    
    /// <summary>
    /// The location for this weapon, or <c>null</c> if the weapon is not installed on any part.
    /// </summary>
    public required PartLocation? Location { get; init; }
    public required int[] Slots { get; init; }
}
