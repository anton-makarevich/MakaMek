using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Represents a critical slot assignment for a component in a specific location
/// </summary>
public record LocationSlotAssignment(
    PartLocation Location,
    int FirstSlot,
    int Length
)
{
    /// <summary>
    /// Gets all slot indices covered by this assignment
    /// </summary>
    public IEnumerable<int> Slots => Enumerable.Range(FirstSlot, Length);
}
