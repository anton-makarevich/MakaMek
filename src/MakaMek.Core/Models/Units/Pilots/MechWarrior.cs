using Sanet.MakaMek.Core.Data.Units;

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

    public Guid Id { get; }

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

    public bool IsConscious { get; private set; } = true;

    /// <summary>
    /// The unit this pilot is currently assigned to, if any
    /// </summary>
    public Unit? AssignedTo { get; set; }

    public MechWarrior(string firstName, string lastName, int? gunnery = null, int? piloting = null)
    {
        Id = Guid.NewGuid();
        FirstName = firstName;
        LastName = lastName;
        Health = DefaultHealth;
        Gunnery = gunnery ?? DefaultGunnery;
        Piloting = piloting ?? DefaultPiloting;
    }
    
    public MechWarrior(PilotData pilotData)
    {
        Id = pilotData.Id;
        FirstName = pilotData.FirstName;
        LastName = pilotData.LastName;
        Health = pilotData.Health;
        Gunnery = pilotData.Gunnery;
        Piloting = pilotData.Piloting;
        Injuries = pilotData.Injuries;
        IsConscious = pilotData.IsConscious;
    }

    public void Hit()
    {
        Injuries++;
    }

    public void Kill()
    {
        Injuries = Health;
    }

    public PilotData ToData()
    {
        return new PilotData
        {
            Id = Id,
            FirstName = FirstName,
            LastName = LastName,
            Gunnery = Gunnery,
            Piloting = Piloting,
            Health = Health,
            Injuries = Injuries,
            IsConscious = IsConscious
        };
    }

    public bool IsDead => Injuries >= Health;
}
