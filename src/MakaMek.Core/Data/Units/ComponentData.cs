using Sanet.MakaMek.Core.Models.Units.Components.Engines;

namespace Sanet.MakaMek.Core.Data.Units;

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
    /// Component-specific state data using discriminated union pattern
    /// </summary>
    public ComponentSpecificData? SpecificData { get; init; }
}

/// <summary>
/// Base class for component-specific state data
/// </summary>
public abstract record ComponentSpecificData;

/// <summary>
/// State data specific to ammunition components
/// </summary>
public record AmmoStateData(int RemainingShots) : ComponentSpecificData;

/// <summary>
/// State data specific to engine components
/// </summary>
public record EngineStateData(int Rating, EngineType Type) : ComponentSpecificData;

/// <summary>
/// State data specific to jump jet components
/// </summary>
public record JumpJetStateData(int JumpMp) : ComponentSpecificData;
