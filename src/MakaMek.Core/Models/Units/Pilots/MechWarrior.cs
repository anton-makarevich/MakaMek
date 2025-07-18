namespace Sanet.MakaMek.Core.Models.Units.Pilots;

/// <summary>
/// Default pilot class for BattleMechs
/// </summary>
public class MechWarrior : IPilot
{
    /// <summary>
    /// Default gunnery skill for Inner Sphere MechWarriors
    /// </summary>
    public const int DefaultGunnery = 4;

    /// <summary>
    /// Default piloting skill for Inner Sphere MechWarriors
    /// </summary>
    public const int DefaultPiloting = 5;

    /// <summary>
    /// Default starting health for MechWarriors
    /// </summary>
    public const int DefaultHealth = 6;

    /// <summary>
    /// First name of the MechWarriors
    /// </summary>
    public string FirstName { get; }

    /// <summary>
    /// Last name of the MechWarriors
    /// </summary>
    public string LastName { get; }

    /// <summary>
    /// Current health of the pilot
    /// </summary>
    public int Health { get; private set; }

    /// <summary>
    /// Gunnery skill. Lower is better
    /// </summary>
    public int Gunnery { get; }

    /// <summary>
    /// Piloting skill. Lower is better
    /// </summary>
    public int Piloting { get; }

    public int Injuries { get; private set; }

    public bool IsUnconscious { get;private set; }

    public MechWarrior(string firstName, string lastName, int? gunnery = null, int? piloting = null)
    {
        FirstName = firstName;
        LastName = lastName;
        Health = DefaultHealth;
        Gunnery = gunnery ?? DefaultGunnery;
        Piloting = piloting ?? DefaultPiloting;
    }

    public void Hit()
    {
        Injuries++;
    }

    public void Kill()
    {
        Injuries = Health;
    }

    public bool IsDead => Injuries >= Health;
}
