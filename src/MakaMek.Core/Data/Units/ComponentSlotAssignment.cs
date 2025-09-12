namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Represents a component assigned to specific critical slots in a location
/// </summary>
public record ComponentSlotAssignment
{
    /// <summary>
    /// The component type
    /// </summary>
    public required MakaMekComponent Component { get; init; }

    /// <summary>
    /// The specific critical slots this component occupies (0-based indexing)
    /// </summary>
    public required int[] Slots { get; init; }

    /// <summary>
    /// Creates a single-slot component assignment
    /// </summary>
    /// <param name="component">The component type</param>
    /// <param name="slot">The slot index (0-based)</param>
    /// <returns>A new ComponentSlotAssignment</returns>
    public static ComponentSlotAssignment Single(MakaMekComponent component, int slot)
    {
        return new ComponentSlotAssignment
        {
            Component = component,
            Slots = [slot]
        };
    }

    /// <summary>
    /// Creates a multi-slot component assignment
    /// </summary>
    /// <param name="component">The component type</param>
    /// <param name="slots">The slot indices (0-based)</param>
    /// <returns>A new ComponentSlotAssignment</returns>
    public static ComponentSlotAssignment Multiple(MakaMekComponent component, params int[] slots)
    {
        return new ComponentSlotAssignment
        {
            Component = component,
            Slots = slots
        };
    }
}