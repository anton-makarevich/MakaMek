namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Represents a component instance with its slot assignments across potentially multiple locations
/// </summary>
public record ComponentData 
{
    /// <summary>
    /// The type of component
    /// </summary>
    public required MakaMekComponent Type { get; init; }
    
    /// <summary>
    /// The slot assignments for this component across all locations
    /// </summary>
    public required List<LocationSlotAssignment> Assignments { get; init; } = new();
    
    // Optional future metadata:
    // public string? Id { get; init; }
    // public int? Quantity { get; init; }
}
