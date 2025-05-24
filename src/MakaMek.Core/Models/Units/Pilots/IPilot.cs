namespace Sanet.MakaMek.Core.Models.Units.Pilots;

/// <summary>
/// Interface for unit pilots/crew members
/// </summary>
public interface IPilot
{
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

    int Injuries { get; }
    bool IsUnconscious { get; }

    void Hit();
}
