namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Data structure for linking pilots to units during game setup
/// </summary>
public record struct PilotAssignmentData
{
    /// <summary>
    /// The ID of the unit to assign the pilot to
    /// </summary>
    public required Guid UnitId { get; init; }
    
    /// <summary>
    /// The pilot data to assign to the unit
    /// </summary>
    public required PilotData PilotData { get; init; }
}
