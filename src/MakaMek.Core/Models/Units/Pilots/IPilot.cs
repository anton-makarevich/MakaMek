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
    /// The turn number when the pilot became unconscious, null if conscious
    /// </summary>
    int? UnconsciousInTurn { get; }

    /// <summary>
    /// Queue of consciousness numbers for pending consciousness rolls
    /// </summary>
    Queue<int> PendingConsciousnessNumbers { get; }

    /// <summary>
    /// Gets the current consciousness number based on injury level
    /// </summary>
    int CurrentConsciousnessNumber { get; }

    string Name { get; }
    
    /// <summary>
    /// The unit this pilot is currently assigned to, if any
    /// </summary>
    Unit? AssignedTo { get; set; }

    /// <summary>
    /// Applies a hit to the pilot, increasing the number of injuries
    /// </summary>
    void Hit(int hits = 1);

    /// <summary>
    /// Kills the pilot, setting injuries to health
    /// </summary>
    void Kill();

    PilotData ToData();
        
    /// <summary>
    /// Applies explosion damage to the pilot, typically from exploding components
    /// Requires a sepaeate method as should be implemented differently for different pilot types
    /// </summary>
    void ExplosionHit();

    /// <summary>
    /// Knocks the pilot unconscious in the specified turn
    /// </summary>
    /// <param name="turn">The turn number when the pilot became unconscious</param>
    void KnockUnconscious(int turn);

    /// <summary>
    /// Recovers the pilot from unconsciousness
    /// </summary>
    void RecoverConsciousness();
}
