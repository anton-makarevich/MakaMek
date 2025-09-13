namespace Sanet.MakaMek.Core.Models.Units.Components;

/// <summary>
/// Represents a critical slot assignment for a component in a specific unit part
/// </summary>
public class CriticalSlotAssignment
{
    /// <summary>
    /// The unit part where this assignment is located
    /// </summary>
    public required UnitPart UnitPart { get; init; }
    
    /// <summary>
    /// The first slot index in this assignment (0-based)
    /// </summary>
    public required int FirstSlot { get; init; }
    
    /// <summary>
    /// The number of consecutive slots in this assignment
    /// </summary>
    public required int Length { get; init; }
    
    /// <summary>
    /// Gets all slot indices covered by this assignment
    /// </summary>
    public int[] Slots => Enumerable.Range(FirstSlot, Length).ToArray();
    
    /// <summary>
    /// Gets the location of this assignment
    /// </summary>
    public PartLocation Location => UnitPart.Location;
    
    /// <summary>
    /// Creates a single-slot assignment
    /// </summary>
    /// <param name="unitPart">The unit part</param>
    /// <param name="slot">The slot index</param>
    /// <returns>A new CriticalSlotAssignment</returns>
    public static CriticalSlotAssignment Single(UnitPart unitPart, int slot)
    {
        return new CriticalSlotAssignment
        {
            UnitPart = unitPart,
            FirstSlot = slot,
            Length = 1
        };
    }
    
    /// <summary>
    /// Creates a multi-slot assignment
    /// </summary>
    /// <param name="unitPart">The unit part</param>
    /// <param name="firstSlot">The first slot index</param>
    /// <param name="length">The number of consecutive slots</param>
    /// <returns>A new CriticalSlotAssignment</returns>
    public static CriticalSlotAssignment Multiple(UnitPart unitPart, int firstSlot, int length)
    {
        return new CriticalSlotAssignment
        {
            UnitPart = unitPart,
            FirstSlot = firstSlot,
            Length = length
        };
    }
}
