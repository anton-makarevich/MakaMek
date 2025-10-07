using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Represents the runtime state of a unit part for serialization
/// Only includes parts that have damage, are destroyed, or are blown off
/// </summary>
public record struct UnitPartStateData
{
    /// <summary>
    /// The location of the part
    /// </summary>
    public required PartLocation Location { get; init; }
    
    /// <summary>
    /// Current front armor level (null means use max armor from UnitData)
    /// </summary>
    public int? CurrentFrontArmor { get; init; }
    
    /// <summary>
    /// Current rear armor level for torso parts (null means use max armor from UnitData)
    /// </summary>
    public int? CurrentRearArmor { get; init; }
    
    /// <summary>
    /// Current structure level (null means use max structure)
    /// </summary>
    public int? CurrentStructure { get; init; }
    
    /// <summary>
    /// Whether the part is blown off
    /// </summary>
    public bool IsBlownOff { get; init; }
}

