using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Units.Components;

/// <summary>
/// Represents a critical slot assignment for a component in a specific location
/// </summary>
public record LocationSlotAssignment(
    PartLocation Location,
    int FirstSlot,
    int Length
);

public static class LocationSlotAssignmentExtensions
{
    public static int[] GetSlots(this LocationSlotAssignment assignment)
    {
        return Enumerable.Range(assignment.FirstSlot, assignment.Length).ToArray();
    }
}