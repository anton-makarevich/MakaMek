using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Models.Map;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class Mech : Unit
{
    public int PossibleTorsoRotation { get; }
    
    public HexDirection? TorsoDirection=> _parts.OfType<Torso>().FirstOrDefault()?.Facing;

    public bool HasUsedTorsoTwist
    {
        get
        {
            if (Position == null) return false;
            var torsos = _parts.OfType<Torso>();
            return torsos.Any(t => t.Facing != Position.Facing);
        }
    }

    public Mech(
        string chassis,
        string model, 
        int tonnage, 
        int walkMp,
        IEnumerable<UnitPart> parts,
        int possibleTorsoRotation = 1,
        Guid? id = null) 
        : base(chassis, model, tonnage, walkMp, parts, id)
    {
        PossibleTorsoRotation = possibleTorsoRotation;
        Status = UnitStatus.Active;
        // Assign a default mechwarrior with a generated name
        var randomId = Guid.NewGuid().ToString()[..6];
        Crew = new MechWarrior($"MechWarrior", randomId);
    }

    public bool CanRotateTorso=> PossibleTorsoRotation > 0 && !HasUsedTorsoTwist;

    public void RotateTorso(HexDirection newFacing)
    {
        if (!CanRotateTorso)
            return;

        var currentUnitFacing = (int)Position!.Facing;
        var newFacingInt = (int)newFacing;
        
        // Calculate steps in both directions (clockwise and counterclockwise)
        var clockwiseSteps = (newFacingInt - currentUnitFacing + 6) % 6;
        var counterClockwiseSteps = (currentUnitFacing - newFacingInt + 6) % 6;
        
        // Use the smaller number of steps
        var steps = Math.Min(clockwiseSteps, counterClockwiseSteps);
        
        // Check if rotation is within the allowed range
        if (steps > PossibleTorsoRotation) return;
        foreach (var torso in _parts.OfType<Torso>())
        {
            torso.Rotate(newFacing);
        }
    }

    public override PartLocation? GetTransferLocation(PartLocation location) => location switch
    {
        PartLocation.LeftArm => PartLocation.LeftTorso,
        PartLocation.RightArm => PartLocation.RightTorso,
        PartLocation.LeftLeg => PartLocation.LeftTorso,
        PartLocation.RightLeg => PartLocation.RightTorso,
        PartLocation.LeftTorso => PartLocation.CenterTorso,
        PartLocation.RightTorso => PartLocation.CenterTorso,
        _ => null
    };

    internal override void ApplyArmorAndStructureDamage(int damage, UnitPart targetPart)
    {
        base.ApplyArmorAndStructureDamage(damage, targetPart);
        var head = _parts.Find(p => p.Location == PartLocation.Head);
        if (head is { IsDestroyed: true })
        {
            Status = UnitStatus.Destroyed;
            return;
        }
        var centerTorso = _parts.Find(p => p.Location == PartLocation.CenterTorso);
        if (centerTorso is { IsDestroyed: true })
        {
            Status = UnitStatus.Destroyed;
        }
    }

    public override int EngineHeatSinks => GetAllComponents<Engine>().FirstOrDefault()?.NumberOfHeatSinks??0;

    /// <summary>
    /// Gets the heat penalty caused by engine damage
    /// </summary>
    public override int EngineHeatPenalty => GetAllComponents<Engine>().FirstOrDefault()?.HeatPenalty ?? 0;

    protected override void ApplyHeatEffects()
    {
        // Apply effects based on the current heat level
        if (CurrentHeat >= 30)
        {
            // Automatic shutdown
            Status = UnitStatus.Shutdown;
        }
        else if (CurrentHeat >= 25)
        {
            // Chance to shut down, ammo explosion, etc.
            // To be implemented
        }
        
        // Movement penalties are calculated on-demand in ModifiedMovement
    }
    
    // Calculate movement penalty based on current heat
    public override int MovementHeatPenalty=> CurrentHeat switch
        {
            >= 25 => 5,
            >= 20 => 4,
            >= 15 => 3,
            >= 10 => 2,
            >= 5 => 1,
            _ => 0
        };
    
    // Calculate attack penalty based on current heat
    public override int AttackHeatPenalty => CurrentHeat switch
        {
            >= 24 => 4,
            >= 17 => 3,
            >= 13 => 2,
            >= 8 => 1,
            _ => 0
        };
    
    public override int CalculateBattleValue()
    {
        var bv = Tonnage * 100; // Base value
        bv += GetAllComponents<Weapon>().Sum(w => w.BattleValue);
        return bv;
    }

    public override bool CanMoveBackward(MovementType type) => type == MovementType.Walk;

    public void SetProne()
    {
        Status |= UnitStatus.Prone;
    }

    public void StandUp()
    {
        Status &= ~UnitStatus.Prone;
    }

    public override HexPosition? Position
    {
        get => base.Position;
        protected set
        {
            base.Position = value;
            // Reset torso rotation when the position changes
            foreach (var torso in _parts.OfType<Torso>())
            {
                torso.ResetRotation();
            }
        }
    }
    
    /// <summary>
    /// Resets the turn state for the mech, including torso rotation
    /// </summary>
    public override void ResetTurnState()
    {
        base.ResetTurnState();
        
        // Reset torso rotation
        foreach (var torso in _parts.OfType<Torso>())
        {
            torso.ResetRotation();
        }
    }

    /// <summary>
    /// Calculates critical hit data for a specific location and damage
    /// </summary>
    /// <param name="location">The hit location</param>
    /// <param name="diceRoller">The dice roller to use for critical hit determination</param>
    /// <returns>Critical hit data or null if no critical hits</returns>
    public override LocationCriticalHitsData? CalculateCriticalHitsData(
        PartLocation location, 
        IDiceRoller diceRoller)
    {
        var part = _parts.FirstOrDefault(p => p.Location == location);
        if (part is not { CurrentStructure: > 0 })
            return null;
            
        var critRoll = diceRoller.Roll2D6().Sum(d => d.Result);
        var numCrits = GetNumCriticalHits(critRoll);
        ComponentHitData[]? hitComponents = null;
        
        // Check if the location can be blown off (head or limbs on a roll of 12)
        var isBlownOff = part.CanBeBlownOff && numCrits == 3;
        if (isBlownOff)
        {
            return new LocationCriticalHitsData(location, critRoll, 0, null, isBlownOff);
        }
        
        if (numCrits > 0)
        {
            hitComponents = DetermineCriticalHitSlots(part, numCrits, diceRoller);
        }
        
        return new LocationCriticalHitsData(location, critRoll, numCrits, hitComponents, isBlownOff);
    }

    /// <summary>
    /// Determines the number of critical hits based on the roll
    /// </summary>
    /// <param name="roll">The 2d6 roll result</param>
    /// <returns>Number of critical hits (0-3)</returns>
    internal int GetNumCriticalHits(int roll)
    {
        // 2–7: 0, 8–9: 1, 10–11: 2, 12: 3 (always return 3 for roll of 12)
        return roll switch
        {
            <= 7 => 0,
            8 or 9 => 1,
            10 or 11 => 2,
            12 => 3,
            _ => 0
        };
    }

    /// <summary>
    /// Determines which specific slots are affected by critical hits
    /// </summary>
    /// <param name="part">The unit part receiving critical hits</param>
    /// <param name="numCriticalHits">Number of critical hits to determine</param>
    /// <param name="diceRoller">The dice roller to use</param>
    /// <returns>Array of slot indices affected by critical hits, or null if none</returns>
    private ComponentHitData[]? DetermineCriticalHitSlots(UnitPart part, int numCriticalHits, IDiceRoller diceRoller)
    {
        var availableSlots = Enumerable.Range(0, part.TotalSlots)
            .Where(slot => !part.HitSlots.Contains(slot)
                           && part.GetComponentAtSlot(slot)!=null)
            .ToList();
        if (availableSlots.Count == 0 || numCriticalHits == 0)
            return null;
        var result = new List<ComponentHitData>();
        for (var i = 0; i < numCriticalHits; i++)
        {
            if (availableSlots.Count == 1)
            {
                result.Add(CreateComponentHitData(part,0));
                availableSlots.RemoveAt(0);
                break;
            }
            var slot = -1;
            // Roll for slot as per 6/12 slot logic
            if (part.TotalSlots <= 6)
            {
                // 1d6, map 1-6 to 0-5
                do {
                    slot = diceRoller.RollD6().Result - 1;
                } while (!availableSlots.Contains(slot));
            }
            else
            {
                int group;
                do {
                    var groupRoll = diceRoller.RollD6().Result; // 1d6
                    group = groupRoll <= 3 ? 0 : 1;
                    var groupSlots = availableSlots.Where(s => group == 0 ? s < 6 : s >= 6).ToList();
                    if (groupSlots.Count == 1)
                    {
                        slot = groupSlots[0];
                    }
                    else if (groupSlots.Count > 1)
                    {
                        do
                        {
                            var slotRoll = diceRoller.RollD6().Result - 1;
                            slot = group == 0 ? slotRoll : slotRoll + 6;
                        } while (!groupSlots.Contains(slot));
                    }
                } while (!availableSlots.Contains(slot));
            }

            if (slot == -1) continue;
            result.Add(CreateComponentHitData(part, slot));
            availableSlots.Remove(slot);
        }
        return result.Count > 0 ? result.ToArray() : null;
    }

    private ComponentHitData CreateComponentHitData(UnitPart part, int slot)
    {
        var component = part.GetComponentAtSlot(slot);
        if (component == null) throw new ArgumentException("Invalid slot");
        return new ComponentHitData()
        {
            Slot = slot,
            Type = component.ComponentType
        }; 
    }
}
