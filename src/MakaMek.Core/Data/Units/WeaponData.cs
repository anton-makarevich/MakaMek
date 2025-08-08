using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Serializable data to identify a specific weapon on a unit
/// </summary>
public record WeaponData
{
    public required string Name { get; init; }
    public required PartLocation? Location { get; init; }
    public required int[] Slots { get; init; }
}
