namespace Sanet.MakaMek.Core.Data.Units;

/// <summary>
/// Data structure for pilot information
/// </summary>
public record struct PilotData
{
    public Guid Id { get; init; }
    /// <summary>
    /// First name of the pilot
    /// </summary>
    public string FirstName { get; init; }
    
    /// <summary>
    /// Last name of the pilot
    /// </summary>
    public string LastName { get; init; }
    
    /// <summary>
    /// Gunnery skill. Lower is better
    /// </summary>
    public int Gunnery { get; init; }
    
    /// <summary>
    /// Piloting skill. Lower is better
    /// </summary>
    public int Piloting { get; init; }
    
    /// <summary>
    /// Current health of the pilot
    /// </summary>
    public int Health { get; init; }
    
    /// <summary>
    /// Number of injuries sustained by the pilot
    /// </summary>
    public int Injuries { get; init; }
    
    /// <summary>
    /// Indicates whether the pilot is conscious
    /// </summary>
    public bool IsConscious { get; init; }

    public static PilotData CreateDefaultPilot(string firstName ,string lastName)
    {
        return new PilotData
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Gunnery = 4,
            Piloting = 5,
            Health = 6,
            Injuries = 0,
            IsConscious = true
        };
    }
}
