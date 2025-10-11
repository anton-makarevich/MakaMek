using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;

namespace Sanet.MakaMek.Core.Models.Units.Mechs;

public class Mech : Unit
{
    private const int StandupCost = 2;

    public int StandupAttempts { private set; get; }
    public int PossibleTorsoRotation { get; }
    
    public HexDirection? TorsoDirection=> _parts.Values.OfType<Torso>().FirstOrDefault()?.Facing;

    public bool HasUsedTorsoTwist
    {
        get
        {
            if (Position == null) return false;
            var torsos = _parts.Values.OfType<Torso>();
            return torsos.Any(t => t.Facing != Position.Facing);
        }
    }

    public Mech(
        string chassis,
        string model,
        int tonnage,
        IEnumerable<UnitPart> parts,
        int possibleTorsoRotation = 1,
        Guid? id = null)
        : base(chassis, model, tonnage, parts, id)
    {
        PossibleTorsoRotation = possibleTorsoRotation;
        Status = UnitStatus.Active;
    }

    public override int GetMovementPoints(MovementType type)
    {
        return type switch
        {
            MovementType.Walk => ModifiedMovement,
            MovementType.Run => (int)Math.Ceiling(ModifiedMovement * 1.5),
            MovementType.Jump =>  GetAvailableComponents<JumpJets>().Sum(j => j.JumpMp),
            _ => 0
        };
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
        foreach (var torso in _parts.Values.OfType<Torso>())
        {
            torso.Rotate(newFacing);
        }
    }

    protected override void UpdateDestroyedStatus()
    {
        if (_parts.TryGetValue(PartLocation.Head, out var head) && head.IsDestroyed)
        {
            Status = UnitStatus.Destroyed;
            return;
        }
        if (_parts.TryGetValue(PartLocation.CenterTorso, out var centerTorso) && centerTorso.IsDestroyed)
        {
            Status = UnitStatus.Destroyed;
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
    
    public override int EngineHeatSinks => GetAllComponents<Engine>().FirstOrDefault()?.NumberOfHeatSinks??0;

    /// <summary>
    /// Gets the heat penalty caused by engine damage
    /// </summary>
    public override EngineHeatPenalty? EngineHeatPenalty => GetAllComponents<Engine>().FirstOrDefault()?.HeatPenalty;

    /// <summary>
    /// Determines if this Mech can fire weapons. Returns false if sensors are destroyed (2 critical hits).
    /// </summary>
    public override bool CanFireWeapons
    {
        get
        {
            if (base.CanFireWeapons == false) return false;
            var sensors = GetAllComponents<Sensors>().FirstOrDefault();
            return sensors?.IsAvailable == true;
        }
    }

    protected override void ApplyHeatEffects()
    {
        // Heat shutdown is now handled by HeatEffectsCalculator in HeatPhase
        // Only apply pilot damage from life support failure here

        var lifeSupport = GetAllComponents<LifeSupport>().FirstOrDefault(ls=>ls.IsDestroyed);
        if (lifeSupport!=null && CurrentHeat >= 15 && Pilot is MechWarrior mw)
        {
            var hits = 1;
            if (CurrentHeat >=26)
            {
                hits++;
            }
            mw.Hit(hits);
        }
    }
    
    // Calculate movement penalty based on current heat
    public override HeatMovementPenalty? MovementHeatPenalty
    {
        get
        {
            if (CurrentHeat < 5) return null;
            var heatPenaltyValue = CurrentHeat switch
            {
                >= 25 => 5,
                >= 20 => 4,
                >= 15 => 3,
                >= 10 => 2,
                >= 5 => 1,
                _ => 0
            };
            return new HeatMovementPenalty
            {
                HeatLevel = CurrentHeat,
                Value = heatPenaltyValue
            };
        }
    }

    public override int DamageReducedMovement
    {
        get
        {
            var movementPenalties = GetMovementModifiers();
            var destroyedLegsPenalty = movementPenalties.OfType<LegDestroyedPenalty>().FirstOrDefault();
            if (destroyedLegsPenalty != null) return 2-destroyedLegsPenalty.DestroyedLegCount; // This will reduce movement to 1 for one destroyed leg
            
            var destroyedHipsPenalty = movementPenalties.OfType<HipDestroyedPenalty>().FirstOrDefault();
            // If both hips are destroyed, movement is 0
            if (destroyedHipsPenalty is { DestroyedHipCount: >= 2 })
                return 0; // This will reduce movement to 0

            var hipModifiedMovement = BaseMovement - (destroyedHipsPenalty?.Value ?? 0);
            
            // Apply actuator penalties (excluding hip penalties which are handled above)
            var actuatorPenalties = GetMovementModifiers()
                .Where(p => p is FootActuatorMovementPenalty 
                    or LowerLegActuatorMovementPenalty 
                    or UpperLegActuatorMovementPenalty)
                .Sum(p => p.Value);

            return Math.Max(0, hipModifiedMovement - actuatorPenalties);
        }
    }
    
    // Calculate attack penalty based on current heat
    public override HeatRollModifier? AttackHeatPenalty
    {
        get
        {
            if (CurrentHeat < 8) return null;

            var heatPenaltyValue = CurrentHeat switch
            {
                >= 24 => 4,
                >= 17 => 3,
                >= 13 => 2,
                >= 8 => 1,
                _ => 0
            };

            return new HeatRollModifier
            {
                HeatLevel = CurrentHeat,
                Value = heatPenaltyValue
            };
        }
    }

    /// <summary>
    /// Gets all movement penalties currently affecting this mech as a property for UI binding
    /// </summary>
    public override IReadOnlyList<RollModifier> MovementModifiers => GetMovementModifiers();

    /// <summary>
    /// Gets all movement penalties currently affecting this mech
    /// </summary>
    private IReadOnlyList<RollModifier> GetMovementModifiers()
    {
        var penalties = new List<RollModifier>();

        // Heat movement penalty
        var heatPenalty = MovementHeatPenalty;
        if (heatPenalty != null)
        {
            penalties.Add(heatPenalty);
        }
        
        // Leg destruction penalty
        var destroyedLegs = _parts.Values.OfType<Leg>().Count(p=> p.IsDestroyed);
        var legDestructionPenalty = LegDestroyedPenalty.Create(destroyedLegs, BaseMovement);
        if (legDestructionPenalty != null)
        {
            penalties.Add(legDestructionPenalty);
            return penalties;
        }

        // Hip actuator penalty
        var destroyedHips = GetAllComponents<HipActuator>().Count(a => a.IsDestroyed);
        var hipDestroyedPenalty = HipDestroyedPenalty.Create(destroyedHips, BaseMovement);
        if (hipDestroyedPenalty != null)
        {
            penalties.Add(hipDestroyedPenalty);
        }

        // Foot actuator penalty
        var destroyedFoot = GetAllComponents<FootActuator>().Count(a => a.IsDestroyed);
        if (destroyedFoot > 0)
        {
            penalties.Add(new FootActuatorMovementPenalty
            {
                DestroyedCount = destroyedFoot,
                Value = destroyedFoot
            });
        }

        // Lower leg actuator penalty
        var destroyedLowerLeg = GetAllComponents<LowerLegActuator>().Count(a => a.IsDestroyed);
        if (destroyedLowerLeg > 0)
        {
            penalties.Add(new LowerLegActuatorMovementPenalty
            {
                DestroyedCount = destroyedLowerLeg,
                Value = destroyedLowerLeg
            });
        }

        // Upper leg actuator penalty
        var destroyedUpperLeg = GetAllComponents<UpperLegActuator>().Count(a => a.IsDestroyed);
        if (destroyedUpperLeg > 0)
        {
            penalties.Add(new UpperLegActuatorMovementPenalty
            {
                DestroyedCount = destroyedUpperLeg,
                Value = destroyedUpperLeg
            });
        }

        return penalties;
    }
    
    /// <summary>
    /// Gets all attack penalties currently affecting this mech
    /// </summary>
    public override IReadOnlyList<RollModifier> GetAttackModifiers(PartLocation location)
    {
        var penalties = new List<RollModifier>();

        // Heat attack penalty
        if (AttackHeatPenalty != null)
        {
            penalties.Add(AttackHeatPenalty);
        }

        // Prone firing penalty
        if (IsProne)
        {
            penalties.Add(new ProneAttackerModifier
            {
                Value = ProneAttackerModifier.DefaultValue // +2 modifier for firing while prone
            });
        }

        // Add sensor hit modifier for Mechs
        var sensors = GetAllComponents<Sensors>().FirstOrDefault(s=>s.Hits>0);
        if (sensors!=null)
        {
            var sensorsHitModifier = SensorHitModifier.Create(sensors.Hits);
            if (sensorsHitModifier!=null) penalties.Add(sensorsHitModifier);
        }

        // Arm critical hit modifiers
        var armCriticalModifiers = GetArmCriticalHitModifiers(location);
        penalties.AddRange(armCriticalModifiers);

        return penalties;
    }

    /// <summary>
    /// Gets all arm critical hit modifiers for the mech
    /// </summary>
    /// <param name="location"></param>
    private IEnumerable<RollModifier> GetArmCriticalHitModifiers(PartLocation location)
    {
        // Check the requested arm
        var arm = _parts.Values.OfType<Arm>().FirstOrDefault(a => a.Location == location && a.IsDestroyed == false);
        if (arm == null) return [];

        var modifiers = new List<RollModifier>();

        // Check for shoulder actuator first (overrides other arm modifiers)
        var shoulder = arm.GetComponents<ShoulderActuator>().FirstOrDefault(sh => sh.IsDestroyed);
        if (shoulder != null)
        {
            modifiers.Add(new ShoulderActuatorHitModifier
            {
                ArmLocation = arm.Location,
                Value = 4 // +4 modifier for destroyed shoulder
            });
            // Skip other checks for this arm as per rules
            return modifiers;
        }

        // Check upper arm actuator
        var upperArm = arm.GetComponents<UpperArmActuator>().FirstOrDefault(a=>a.IsDestroyed);
        if (upperArm != null)
        {
            modifiers.Add(new UpperArmActuatorHitModifier
            {
                ArmLocation = arm.Location,
                Value = 1 // +1 modifier for destroyed upper arm actuator
            });
        }

        // Check lower arm actuator
        var lowerArm = arm.GetComponents<LowerArmActuator>().FirstOrDefault(a=>a.IsDestroyed);
        if (lowerArm != null)
        {
            modifiers.Add(new LowerArmActuatorHitModifier
            {
                ArmLocation = arm.Location,
                Value = 1 // +1 modifier for destroyed lower arm actuator
            });
        }

        return modifiers;
    }

    public override int CalculateBattleValue()
    {
        var bv = Tonnage * 100; // Base value
        bv += GetAllComponents<Weapon>().Sum(w => w.BattleValue);
        return bv;
    }

    public override bool CanMoveBackward(MovementType type) => type == MovementType.Walk;

    public override bool IsMinimumMovement
    {
        get
        {
            if (IsProne 
                && GetMovementPoints(MovementType.Walk) == 1 
                && StandupAttempts == 0) return true;
            return false;
        }
    }

    public bool CanJump
    {
        get
        {
            // Cannot jump if currently prone
            if (IsProne) return false;

            // Cannot jump if the mech stood up in this phase
            if (StandupAttempts > 0) return false;

            // Cannot jump if no functional jump jets are available
            if (GetMovementPoints(MovementType.Jump) < 1) return false;

            return true;
        }
    }
    
    public bool CanRun {
        get
        {
            var destroyedLegs = _parts.Values.OfType<Leg>().Count(p=> p.IsDestroyed || p.IsBlownOff);
            if (destroyedLegs > 0) return false;
            return true;
        }
    }

    /// <summary>
    /// Determines if a piloting skill roll is required for jumping due to damage
    /// </summary>
    public bool IsPsrForJumpRequired()
    {
        var destroyedFootActuators = GetAllComponents<FootActuator>()
            .Where(a=>a.IsDestroyed);
        if (destroyedFootActuators.Any()) return true;
        var destroyedHipActuators = GetAllComponents<HipActuator>()
            .Where(a=>a.IsDestroyed);
        if (destroyedHipActuators.Any()) return true;
        var destroyedLowerLegActuators = GetAllComponents<LowerLegActuator>()
            .Where(a=>a.IsDestroyed);
        if (destroyedLowerLegActuators.Any()) return true;
        var destroyedUpperLegActuators = GetAllComponents<UpperLegActuator>()
            .Where(a=>a.IsDestroyed);
        if (destroyedUpperLegActuators.Any()) return true;
        
        var gyro = GetAvailableComponents<Gyro>().FirstOrDefault();
        return gyro?.Hits ==1;
    }

    public void SetProne()
    {
        Status |= UnitStatus.Prone;
    }

    public bool CanStandup()
    {
        if (IsShutdown) return false;
        
        if (!IsGyroAvailable) return false;

        var destroyedLegs = _parts.Values.OfType<Leg>().Count(p=> p.IsDestroyed);
        if (destroyedLegs >= 2) return false;

        // Check if the Mech has at least one movement point available
        if (GetMovementPoints(MovementType.Walk) < 1) return false;

        if (Pilot?.IsConscious == false) return false;

        return true;
    }

    private bool IsGyroAvailable {
        get
        {
            var gyro = GetAvailableComponents<Gyro>().FirstOrDefault();
            return gyro != null;
        }
    }

    /// <summary>
    /// Determines if the mech can change its facing while prone
    /// </summary>
    public bool CanChangeFacingWhileProne()
    {
        // Must be prone to use this action
        if (!IsProne) return false;

        // Cannot change facing if shutdown
        if (IsShutdown) return false;

        // Must have at least 1 movement point available
        if (GetMovementPoints(MovementType.Walk) < 1) return false;
        
        return true;
    }

    public void StandUp(HexDirection newFacing)
    {
        Status &= ~UnitStatus.Prone;

        // update the mech's facing
        if (Position != null) Position = Position with { Facing = newFacing };
    }

    public void AttemptStandup()
    {
        StandupAttempts++;
        var pointsToSpend = Math.Min(GetMovementPoints(MovementType.Walk), StandupCost);
        SpendMovementPoints(pointsToSpend);
    }
    
    public override HexPosition? Position
    {
        get => base.Position;
        protected set
        {
            base.Position = value;
            // Reset torso rotation when the position changes
            foreach (var torso in _parts.Values.OfType<Torso>())
            {
                torso.ResetRotation();
            }
        }
    }

    /// <summary>
    /// Determines if the mech is immobile based on the rules:
    /// </summary>
    public override bool IsImmobile
    {
        get
        {   // A mech is immobile if it's shutdown
            if (IsShutdown)
                return true;

            // A mech with an unconscious pilot is immobile
            if (Pilot == null || Pilot.IsConscious == false)
                return true;

            // A mech with both legs and both arms destroyed/blown off is immobile
            var destroyedLegs = _parts.Values.OfType<Leg>().Count(leg => leg.IsDestroyed);
            if (destroyedLegs < 2) return false;
            var destroyedArms = _parts.Values.OfType<Arm>().Count(arm => arm.IsDestroyed);
            if (destroyedArms >= 2)
                return true;

            return false;
        }
    }

    public bool IsProne => (Status & UnitStatus.Prone) == UnitStatus.Prone;

    /// <summary>
    /// Resets the turn state for the mech, including torso rotation
    /// </summary>
    public override void ResetTurnState()
    {
        base.ResetTurnState();

        StandupAttempts = 0;
        
        // Reset torso rotation
        foreach (var torso in _parts.Values.OfType<Torso>())
        {
            torso.ResetRotation();
        }
    }

    /// <summary>
    /// Calculates critical hit data for a specific location and damage
    /// </summary>
    /// <param name="location">The hit location</param>
    /// <param name="diceRoller">The dice roller to use for critical hit determination</param>
    /// <param name="damageTransferCalculator">Damage transfer calculator to calculate damage distribution</param>
    /// <returns>Critical hit data or null if no critical hits</returns>
    public override LocationCriticalHitsData? CalculateCriticalHitsData(
        PartLocation location, 
        IDiceRoller diceRoller,
        IDamageTransferCalculator damageTransferCalculator)
    {
        _parts.TryGetValue(location, out var part);
        if (part is not { CurrentStructure: > 0 })
            return null;
            
        var critRoll = diceRoller.Roll2D6()
            .Select(d => d.Result)
            .ToArray();
        var numCrits = GetNumCriticalHits(critRoll.Sum());
        ComponentHitData[]? hitComponents = null;
        
        // Check if the location can be blown off (head or limbs on a roll of 12)
        var isBlownOff = part.CanBeBlownOff && numCrits == 3;
        if (isBlownOff)
        {
            return new LocationCriticalHitsData(location, critRoll, 0, null, isBlownOff);
        }
        
        if (numCrits > 0)
        {
            hitComponents = DetermineCriticalHitSlots(part, numCrits, diceRoller, damageTransferCalculator);
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
    /// <param name="damageTransferCalculator">Damage transfer calculator to calculate damage distribution</param>
    /// <returns>Array of slot indices affected by critical hits, or null if none</returns>
    private ComponentHitData[]? DetermineCriticalHitSlots(UnitPart part,
        int numCriticalHits,
        IDiceRoller diceRoller,
        IDamageTransferCalculator damageTransferCalculator)
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
                result.Add(ComponentHitData.CreateComponentHitData(part,availableSlots[0], damageTransferCalculator));
                availableSlots.RemoveAt(0);
                break;
            }
            var slot = -1;
            // Roll for slot as per 6/12 slot logic
            if (availableSlots.Count <= 6)
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
            result.Add(ComponentHitData.CreateComponentHitData(part, slot, damageTransferCalculator));
            availableSlots.Remove(slot);
        }
        return result.Count > 0 ? result.ToArray() : null;
    }
}
