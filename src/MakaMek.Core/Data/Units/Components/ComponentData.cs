namespace Sanet.MakaMek.Core.Data.Units.Components;

/// <summary>
/// Represents a component instance with its slot assignments and mutable state
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

    /// <summary>
    /// Number of hits this component has taken
    /// </summary>
    public int Hits { get; init; } = 0;

    /// <summary>
    /// Whether this component is currently active
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Whether this component has exploded (for explosive components)
    /// </summary>
    public bool HasExploded { get; init; } = false;

    /// <summary>
    /// Override name for this specific component instance (optional)
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Manufacturer of this specific component instance (optional, falls back to definition default)
    /// </summary>
    public string? Manufacturer { get; init; }

    /// <summary>
    /// Component-specific state data using discriminated union pattern
    /// </summary>
    public ComponentSpecificData? SpecificData { get; init; }
}