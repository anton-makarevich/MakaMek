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
}
