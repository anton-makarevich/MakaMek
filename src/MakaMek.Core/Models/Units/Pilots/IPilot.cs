using Sanet.MakaMek.Core.Data.Units;

namespace Sanet.MakaMek.Core.Models.Units.Pilots;

/// <summary>
/// Interface for unit pilots/crew members
/// </summary>
public interface IPilot
{
    Guid Id { get; }

    /// <summary>
    /// Current health of the pilot
    /// </summary>
    int Health { get; }

    /// <summary>
    /// Gunnery skill. Lower is better
    /// </summary>
    int Gunnery { get; }

    /// <summary>
    /// Piloting skill. Lower is better
    /// </summary>
    int Piloting { get; }

    /// <summary>
    /// Number of injuries sustained by the pilot
    /// </summary>
    int Injuries { get; }
    
    /// <summary>
    /// Indicates whether the pilot is unconscious
    /// </summary>
    bool IsConscious { get; }
    
    /// <summary>
    /// Indicates whether the pilot is dead
    /// </summary>
    bool IsDead { get; }

    /// <summary>
    /// Applies a hit to the pilot, increasing the number of injuries
    /// </summary>
    void Hit();
    
    /// <summary>
    /// Kills the pilot, setting injuries to health
    /// </summary>
    void Kill();

    PilotData ToData();
}
