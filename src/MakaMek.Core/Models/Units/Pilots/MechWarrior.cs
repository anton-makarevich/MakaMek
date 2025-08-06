using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Events;

namespace Sanet.MakaMek.Core.Models.Units.Pilots;

/// <summary>
/// Default pilot class for BattleMechs
/// </summary>
public class MechWarrior : IPilot
{
    private int _injuries;
    private const int MechWarriorExplosionDamage = 2;
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
    /// Optional call sign or nickname of the MechWarrior
    /// </summary>
    public string CallSign { get; }
    
    public string Name =>
        string.IsNullOrEmpty(CallSign) 
            ? $"{FirstName} {LastName}" 
            : $"{FirstName} \"{CallSign}\" {LastName}";

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

    public int Injuries
    {
        get => _injuries;
        private set
        {
            _injuries = value;
            PendingConsciousnessNumbers.Enqueue(CurrentConsciousnessNumber);
        }
    }

    public bool IsConscious { get; private set; } = true;

    public int? UnconsciousInTurn { get; private set; }

    public Queue<int> PendingConsciousnessNumbers { get; private set; } = new();

    /// <summary>
    /// Gets the current consciousness number based on injury level
    /// BattleTech consciousness table: 1 injury→3, 2→5, 3→7, 4→10, 5→11, 6→Dead
    /// </summary>
    public int CurrentConsciousnessNumber
    {
        get
        {
            return Injuries switch
            {
                1 => 3,
                2 => 5,
                3 => 7,
                4 => 10,
                5 => 11,
                >= 6 => 12, // Impossible roll (dead)
                _ => 1 // No injuries, always conscious
            };
        }
    }

    /// <summary>
    /// The unit this pilot is currently assigned to, if any
    /// </summary>
    public Unit? AssignedTo { get; set; }

    public MechWarrior(string firstName, string lastName, string callSign = "", int? gunnery = null, int? piloting = null)
    {
        Id = Guid.NewGuid();
        FirstName = firstName;
        LastName = lastName;
        CallSign = callSign;
        Health = DefaultHealth;
        Gunnery = gunnery ?? DefaultGunnery;
        Piloting = piloting ?? DefaultPiloting;
    }
    
    public MechWarrior(PilotData pilotData)
    {
        Id = pilotData.Id;
        FirstName = pilotData.FirstName;
        LastName = pilotData.LastName;
        CallSign = string.Empty; // Default to empty string for data constructor
        Health = pilotData.Health;
        Gunnery = pilotData.Gunnery;
        Piloting = pilotData.Piloting;
        Injuries = pilotData.Injuries;
        IsConscious = pilotData.IsConscious;
        UnconsciousInTurn = pilotData.UnconsciousInTurn;
        PendingConsciousnessNumbers = new Queue<int>();
    }

    public void Hit(int hits = 1)
    {
        for (var i = 0; i < hits; i++)
        {
            Injuries++;
        }

        AssignedTo?.AddEvent(new UiEvent(UiEventType.PilotDamage, FirstName, hits));
    }

    public void Kill()
    {
        Injuries = Health;
        AssignedTo?.AddEvent(new UiEvent(UiEventType.PilotDead, FirstName));
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
            IsConscious = IsConscious,
            UnconsciousInTurn = UnconsciousInTurn
        };
    }

    public void ExplosionHit()
    {
        Hit(MechWarriorExplosionDamage);
    }

    public bool IsDead => Injuries >= Health;

    public void KnockUnconscious(int turn)
    {
        if (IsDead) return; // Dead pilots can't be unconscious

        IsConscious = false;
        UnconsciousInTurn = turn;
        AssignedTo?.AddEvent(new UiEvent(UiEventType.PilotUnconscious, FirstName));
    }

    public void RecoverConsciousness()
    {
        if (IsDead) return; // Dead pilots can't recover

        IsConscious = true;
        UnconsciousInTurn = null;
        AssignedTo?.AddEvent(new UiEvent(UiEventType.PilotRecovered, FirstName));
    }
}
