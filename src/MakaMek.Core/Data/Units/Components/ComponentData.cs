using System.Text.Json.Serialization;

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
    public required IReadOnlyList<LocationSlotAssignment> Assignments { get; init; }

    /// <summary>
    /// Number of hits this component has taken
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Hits { get; init; }

    /// <summary>
    /// Whether this component is currently active
    /// </summary>
    public bool IsActive { get; init; } = true; 
    
    /// <summary>
    /// Whether this component has exploded (for explosive components)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool HasExploded { get; init; } // Default omitted when false

    /// <summary>
    /// Override name for this specific component instance (optional)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>
    /// Manufacturer of this specific component instance (optional, falls back to definition default)
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Manufacturer { get; init; }

    /// <summary>
    /// Component-specific state data using discriminated union pattern
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ComponentSpecificData? SpecificData { get; init; }
}