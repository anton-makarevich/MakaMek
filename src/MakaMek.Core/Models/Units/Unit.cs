using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Pilots;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Models.Units;

public abstract class Unit
{
    protected readonly List<UnitPart> _parts;
    private readonly Queue<UiEvent> _notifications = new();
    private readonly List<UiEvent> _events = [];

    protected Unit(string chassis, string model, int tonnage,
        int walkMp,
        IEnumerable<UnitPart> parts,
        Guid? id = null)
    {
        Chassis = chassis;
        Model = model;
        Name = $"{chassis} {model}";
        Tonnage = tonnage;
        BaseMovement = walkMp;
        _parts = parts.ToList();
        // Set the Unit reference for each part
        foreach (var part in _parts)
        {
            part.Unit = this;
        }

        if (id.HasValue)
        {
            Id = id.Value;
        }
    }

    public string Chassis { get; }
    public string Model { get; }
    public string Name { get; }
    public int Tonnage { get; }

    public IPlayer? Owner { get; internal set; }

    private UnitStatus _status;

    public UnitStatus Status
    {
        get => _status;
        protected set
        {
            if ((value & UnitStatus.Destroyed) == UnitStatus.Destroyed)
            {
                _status = UnitStatus.Destroyed;
                return;
            }

            // Once destroyed, prevent any further status changes
            if (IsDestroyed)
                return;

            _status = value;
        }
    }

    /// <summary>
    /// Gets whether the unit is destroyed. Returns true if the Destroyed flag is set, regardless of other flags.
    /// </summary>
    public bool IsDestroyed => (_status & UnitStatus.Destroyed) == UnitStatus.Destroyed;

    /// <summary>
    /// Gets whether the unit is active. Returns true if the Active flag is set, regardless of other flags.
    /// </summary>
    public bool IsActive => (_status & UnitStatus.Active) == UnitStatus.Active;

    /// <summary>
    /// Gets whether the unit is shutdown. Returns true if the Shutdown flag is set, regardless of other flags.
    /// </summary>
    public bool IsShutdown => (_status & UnitStatus.Shutdown) == UnitStatus.Shutdown;

    /// <summary>
    /// Gets whether the unit is immobile. Returns true if the Immobile flag is set, regardless of other flags.
    /// </summary>
    public bool IsImmobile => (_status & UnitStatus.Immobile) == UnitStatus.Immobile;

    public bool IsOutOfCommission => IsDestroyed || Pilot?.IsDead == true;

    public WeightClass Class => Tonnage switch
    {
        <= 35 => WeightClass.Light,
        <= 55 => WeightClass.Medium,
        <= 75 => WeightClass.Heavy,
        <= 100 => WeightClass.Assault,
        _ => WeightClass.Unknown
    };

    // Base movement (walking)
    protected int BaseMovement { get; }
    
    // Modified movement after applying effects (defaults to base movement)
    protected int ModifiedMovement => Math.Max(0, DamageReducedMovement 
        - (MovementHeatPenalty?.Value ?? 0) 
        - MovementPointsSpent);
    public virtual int DamageReducedMovement => BaseMovement;

    // Movement heat penalty
    public virtual HeatMovementPenalty? MovementHeatPenalty => null;
    
    // Attack heat penalty
    public virtual HeatRollModifier? AttackHeatPenalty => null;
    
    // Engine heat penalty due to engine damage
    public virtual EngineHeatPenalty? EngineHeatPenalty => null;
    
    public virtual IReadOnlyList<RollModifier> MovementModifiers => [];

    public virtual IReadOnlyList<RollModifier> GetAttackModifiers(PartLocation location)
    {
        return [];
    }

    // Movement capabilities
    public virtual int GetMovementPoints(MovementType _)
    {
        return ModifiedMovement;
    }

    /// <summary>
    /// Determines if the unit can move backward with the given movement type
    /// </summary>
    public abstract bool CanMoveBackward(MovementType type);

    // Location and facing
    public virtual HexPosition? Position { get; protected set; }

    public bool IsDeployed => Position != null;

    public void Deploy(HexPosition position)
    {
        if (Position != null)
        {
            throw new InvalidOperationException($"{Name} is already deployed.");
        }
        Position = position;
    }

    // Heat management
    public int CurrentHeat { get; protected set; }
    public int HeatDissipation => GetAvailableComponents<HeatSink>().Sum(hs => hs.HeatDissipation)
                                  + EngineHeatSinks; // Engine heat sinks

    public virtual int EngineHeatSinks => 0;

    /// <summary>
    /// Calculates and returns heat data for this unit
    /// </summary>
    /// <returns>A HeatData object containing all heat sources and dissipation information</returns>
    public HeatData GetHeatData(IRulesProvider rulesProvider)
    {
        var movementHeatSources = new List<MovementHeatData>();
        var weaponHeatSources = new List<WeaponHeatData>();
        
        // Calculate movement heat
        if (MovementTypeUsed.HasValue)
        {
            var movementHeatPoints = rulesProvider.GetMovementHeatPoints(MovementTypeUsed.Value, MovementPointsSpent);
                
            if (movementHeatPoints > 0)
            {
                movementHeatSources.Add(new MovementHeatData
                {
                    MovementType = MovementTypeUsed.Value,
                    MovementPointsSpent = MovementPointsSpent,
                    HeatPoints = movementHeatPoints
                });
            }
        }
        
        // Calculate weapon heat for weapons with targets
        var weaponsWithTargets = GetAllComponents<Weapon>()
            .Where(weapon => weapon.Target != null);
            
        foreach (var weapon in weaponsWithTargets)
        {
            if (weapon.Heat <= 0) continue;
            weaponHeatSources.Add(new WeaponHeatData
            {
                WeaponName = weapon.Name,
                HeatPoints = weapon.Heat
            });
        }
        
        // Get heat dissipation
        var heatSinks = GetAvailableComponents<HeatSink>().Count();
        var engineHeatSinks = EngineHeatSinks;
        var heatDissipation = HeatDissipation;
        var dissipationData = new HeatDissipationData
        {
            HeatSinks = heatSinks,
            EngineHeatSinks = engineHeatSinks,
            DissipationPoints = heatDissipation
        };
        
        return new HeatData
        {
            MovementHeatSources = movementHeatSources,
            WeaponHeatSources = weaponHeatSources,
            DissipationData = dissipationData,
            EngineHeatSource = EngineHeatPenalty
        };
    }
    
    public void ApplyHeat(HeatData heatData)
    {
        CurrentHeat = Math.Max(0,
            CurrentHeat 
            + heatData.TotalHeatPoints 
            - heatData.TotalHeatDissipationPoints);
        ApplyHeatEffects();
        HasAppliedHeat = true;
    }

    protected abstract void ApplyHeatEffects();
    
    // Parts management
    public IReadOnlyList<UnitPart> Parts =>_parts;
    public Guid Id { get; private set; } = Guid.Empty;
    public IPilot? Pilot { get; protected set; }

    /// <summary>
    /// Assigns a pilot to this unit
    /// </summary>
    /// <param name="pilot">The pilot to assign</param>
    public void AssignPilot(IPilot pilot)
    {
        // If this unit already has a pilot, unassign it first
        if (Pilot is not null)
        {
            Pilot.AssignedTo = null;
        }

        // If the new pilot is already assigned to another unit, unassign it first
        pilot.AssignedTo?.UnassignPilot();

        // Assign the pilot to this unit
        Pilot = pilot;

        // Set the bidirectional relationship
        Pilot.AssignedTo = this;
    }

    /// <summary>
    /// Unassigns the current pilot from this unit
    /// </summary>
    public void UnassignPilot()
    {
        if (Pilot?.AssignedTo != null) Pilot.AssignedTo = null;
        Pilot = null;
    }

    // Armor and Structure totals
    public int TotalMaxArmor => _parts.Sum(p => p.MaxArmor);
    public int TotalCurrentArmor => _parts.Sum(p => p.CurrentArmor);
    public int TotalMaxStructure => _parts.Sum(p => p.MaxStructure);
    public int TotalCurrentStructure => _parts.Sum(p => p.CurrentStructure);

    // Movement tracking
    public int MovementPointsSpent { get; private set; }
    public MovementType? MovementTypeUsed { get; private set; }
    public int DistanceCovered { get; private set; }

    public bool HasMoved => MovementTypeUsed.HasValue;

    // Damage tracking
    public int TotalPhaseDamage { get; private set; }
    
    /// <summary>
    /// Indicates whether this unit has declared weapon attacks for the current turn
    /// </summary>
    public bool HasDeclaredWeaponAttack { get; protected set; }
    
    /// <summary>
    /// Indicates whether this unit has applied heat for the current turn
    /// </summary>
    public bool HasAppliedHeat { get; protected set; }

    private void ResetMovement()
    { 
        MovementPointsSpent = 0;
        MovementTypeUsed = null;
        DistanceCovered = 0;
    }
    
    /// <summary>
    /// Resets the turn state for the unit
    /// </summary>
    public virtual void ResetTurnState()
    {
        ResetMovement();
        HasAppliedHeat = false;
        ResetWeaponsTargets();
        ClearEvents();
    }

    /// <summary>
    /// Resets the phase state for the unit
    /// </summary>
    public void ResetPhaseState()
    {
        TotalPhaseDamage = 0;
    }

    private void ResetWeaponsTargets()
    {
        var weapons = GetAllComponents<Weapon>();
        foreach (var weapon in weapons)
        {
            weapon.Target = null;
        }
        HasDeclaredWeaponAttack = false;
    }

    /// <summary>
    /// Declares weapon attacks against target units
    /// </summary>
    /// <param name="weaponTargets">The weapon target data containing weapon locations, slots and target IDs</param>
    /// <param name="targetUnits">The list of target units</param>
    public void DeclareWeaponAttack(List<WeaponTargetData> weaponTargets, List<Unit> targetUnits)
    {
        if (!IsDeployed)
        {
            throw new InvalidOperationException("Unit is not deployed.");
        }
        
        foreach (var weaponTarget in weaponTargets)
        {
            // Find the weapon at the specified location and slots
            var weapon = GetMountedComponentAtLocation<Weapon>(
                weaponTarget.Weapon.Location, 
                weaponTarget.Weapon.Slots);
                
            if (weapon == null) continue;
            
            // Find the target unit
            var targetUnit = targetUnits.FirstOrDefault(u => u.Id == weaponTarget.TargetId);
            if (targetUnit == null) continue;
            
            // Assign the target to the weapon
            weapon.Target = targetUnit;
        }
        
        // Mark that this unit has declared weapon attacks
        HasDeclaredWeaponAttack = true;
    }
    
    // Methods
    public abstract int CalculateBattleValue();
    
    // Status management
    public virtual void Startup()
    {
        if (!IsShutdown) return;
        Status &= ~UnitStatus.Shutdown;
        Status |= UnitStatus.Active;
    }

    /// <summary>
    /// Determines if this unit can fire weapons. Override in derived classes for specific rules.
    /// </summary>
    public virtual bool CanFireWeapons => true;

    public virtual void Shutdown()
    {
        if (!IsActive) return;
        Status &= ~UnitStatus.Active;
        Status |= UnitStatus.Shutdown;
    }

    public void ApplyDamage(List<HitLocationData> hitLocations)
    {
        foreach (var hitLocation in hitLocations)
        {
            var targetPart = _parts.Find(p => p.Location == hitLocation.Location);
            if (targetPart == null) continue;

            // Calculate total damage including any potential explosion damage
            var totalDamage = hitLocation.Damage;

            // Handle critical hits if present
            if (hitLocation.CriticalHits != null && hitLocation.CriticalHits.Count != 0)
            {
                // Apply all critical hits for all locations in the damage chain
                foreach (var criticalHit in hitLocation.CriticalHits)
                {
                    var criticalPart = _parts.Find(p => p.Location == criticalHit.Location);
                    if (criticalPart == null) continue;

                    // Handle blown off parts
                    if (criticalHit.IsBlownOff)
                    {
                        criticalPart.BlowOff();
                        continue;
                    }

                    // Check for explodable components before applying critical hits
                    if (criticalHit.HitComponents != null)
                    {
                        var explosionDamage = 0;

                        foreach (var componentData in criticalHit.HitComponents)
                        {
                            var slot = componentData.Slot;
                            var component = criticalPart.GetComponentAtSlot(slot);
                            if (component is { CanExplode: true, HasExploded: false })
                            {
                                // Add explosion damage to the total
                                explosionDamage += component.GetExplosionDamage();
                                // Add explosion event
                                AddEvent(new UiEvent(UiEventType.Explosion, component.Name));
                            }
                        }

                        // Add explosion damage to total damage
                        if (explosionDamage > 0)
                        {
                            totalDamage += explosionDamage;
                        }
                    }
                }
            }

            // Track total damage for this phase
            TotalPhaseDamage += totalDamage;

            // Apply the total damage (including any explosion damage)
            ApplyArmorAndStructureDamage(totalDamage, targetPart);

            // Now apply the critical hits after calculating total damage
            if (hitLocation.CriticalHits == null || !hitLocation.CriticalHits.Any()) continue;

            foreach (var criticalHit in hitLocation.CriticalHits)
            {
                var criticalPart = _parts.Find(p => p.Location == criticalHit.Location);
                if (criticalPart == null) continue;

                // Skip blown off parts as they were already handled
                if (criticalHit.IsBlownOff) continue;

                // Apply critical hits to specific slots
                if (criticalHit.HitComponents == null) continue;
                foreach (var component in criticalHit.HitComponents)
                {
                    criticalPart.CriticalHit(component.Slot);
                }
            }


        }

        if (IsDestroyed)
        {
            AddEvent(new UiEvent(UiEventType.UnitDestroyed, Name));
        }
    }

    internal virtual void ApplyArmorAndStructureDamage(int damage, UnitPart targetPart)
    {
        var remainingDamage = targetPart.ApplyDamage(damage);
        
        // If there's remaining damage, transfer to the connected part
        if (remainingDamage <= 0) return;
        var transferLocation = GetTransferLocation(targetPart.Location);
        if (!transferLocation.HasValue) return;
        var transferPart = _parts.Find(p => p.Location == transferLocation.Value);
        if (transferPart != null)
        {
            ApplyArmorAndStructureDamage(remainingDamage, transferPart);
        }
    }

    // Different unit types will have different damage transfer patterns
    public abstract PartLocation? GetTransferLocation(PartLocation location);

    public IEnumerable<T> GetAllComponents<T>() where T : Component
    {
        return Parts.SelectMany(p => p.GetComponents<T>());
    }
    
    public IEnumerable<T> GetAvailableComponents<T>() where T : Component
    {
        return GetAllComponents<T>().Where(c => c.IsAvailable).ToList();
    }

    public bool HasAvailableComponent<T>() where T : Component
    {
        return GetAllComponents<T>().Any(c => c is { IsActive: true, IsAvailable: true });
    }
    
    /// <summary>
    /// Gets all ammo components compatible with the specified weapon
    /// </summary>
    /// <param name="weapon">The weapon to find ammo for</param>
    /// <returns>A collection of ammo components that can be used by the weapon</returns>
    public IEnumerable<Ammo> GetAmmoForWeapon(Weapon weapon)
    {
        if (!weapon.RequiresAmmo)
            return [];
            
        return GetAllComponents<Ammo>()
            .Where(a => a.ComponentType == weapon.AmmoType && a.IsAvailable);
    }
    
    /// <summary>
    /// Gets the total number of remaining shots for a specific weapon
    /// </summary>
    /// <param name="weapon">The weapon to check ammo for</param>
    /// <returns>The total number of remaining shots, or -1 if the weapon doesn't require ammo</returns>
    public int GetRemainingAmmoShots(Weapon weapon)
    {
        if (!weapon.RequiresAmmo)
            return -1;
            
        return GetAmmoForWeapon(weapon).Sum(a => a.RemainingShots);
    }
    
    /// <summary>
    /// Gets all components at a specific location
    /// </summary>
    /// <param name="location">The location to check</param>
    /// <returns>All components at the specified location</returns>
    public IEnumerable<Component> GetComponentsAtLocation(PartLocation location)
    {
        var part = _parts.FirstOrDefault(p => p.Location == location);
        return part?.Components ?? [];
    }

    /// <summary>
    /// Gets components of a specific type at a specific location
    /// </summary>
    /// <typeparam name="T">The type of component to find</typeparam>
    /// <param name="location">The location to check</param>
    /// <returns>All components of the specified type at the specified location</returns>
    public IEnumerable<T> GetComponentsAtLocation<T>(PartLocation location) where T : Component
    {
        var part = _parts.FirstOrDefault(p => p.Location == location);
        return part?.GetComponents<T>() ?? [];
    }
    
    /// <summary>
    /// Gets components of a specific type at a specific location and slots
    /// </summary>
    /// <typeparam name="T">The type of component to find</typeparam>
    /// <param name="location">The location to check</param>
    /// <param name="slots">The slots where the component is mounted</param>
    /// <returns>Components of the specified type at the specified location and slots</returns>
    public T? GetMountedComponentAtLocation<T>(PartLocation location, int[] slots) where T : Component
    {
        if (slots.Length == 0)
            return null;
        var components = GetComponentsAtLocation<T>(location);
  
        return components.FirstOrDefault(c => 
           c.MountedAtSlots.SequenceEqual(slots));
    }

    /// <summary>
    /// Finds the part that contains a specific component
    /// </summary>
    /// <param name="component">The component to find</param>
    /// <returns>The part containing the component, or null if not found</returns>
    public UnitPart? FindComponentPart(Component component)
    {
        // First check the component's MountedOn property
        if (component.MountedOn != null && _parts.Contains(component.MountedOn))
        {
            return component.MountedOn;
        }
        
        // Fallback to searching all parts
        return _parts.FirstOrDefault(p => p.Components.Contains(component));
    }

    public void Move(MovementType movementType, List<PathSegmentData> movementPath)
    {
        if (Position == null)
        {
            throw new InvalidOperationException("Unit is not deployed.");
        } 
        var position = movementType==MovementType.StandingStill
            ? Position
            :new HexPosition(movementPath.Last().To);
        var distance = Position.Coordinates.DistanceTo(position.Coordinates);
        DistanceCovered = distance;
        SpendMovementPoints(movementPath.Sum(s=>s.Cost));
        MovementTypeUsed = movementType;
        Position = position; 
    }
    
    /// <summary>
    /// Fires a weapon based on the provided weapon data.
    /// This applies heat to the unit and consumes ammo if required.
    /// </summary>
    /// <param name="weaponData">Data identifying the weapon to fire</param>
    public void FireWeapon(WeaponData weaponData)
    {
        // Find the weapon using the location and slots from weaponData
        var weapon = GetMountedComponentAtLocation<Weapon>(
            weaponData.Location, 
            weaponData.Slots);
            
        if (weapon is not { IsAvailable: true })
            return;
        
        // If the weapon requires ammo, find and use ammo
        if (!weapon.RequiresAmmo) return;
        // Get all available ammo of the correct type
        var availableAmmo = GetAmmoForWeapon(weapon)
            .Where(a => a.RemainingShots > 0)
            .ToList();
                
        if (availableAmmo.Count == 0)
            return; // No ammo available
                
        // Find the ammo with the most remaining shots
        var ammo = availableAmmo
            .OrderByDescending(a => a.RemainingShots)
            .First();
                
        // Use a shot from the ammo
        ammo.UseShot();
    }

    /// <summary>
    /// Calculates critical hit data for a specific location and damage
    /// </summary>
    /// <param name="location">The hit location</param>
    /// <param name="diceRoller">The dice roller to use for critical hit determination</param>
    /// <returns>Critical hit data or null if no critical hits</returns>
    public abstract LocationCriticalHitsData? CalculateCriticalHitsData(
        PartLocation location, 
        IDiceRoller diceRoller);
    
    // UI events queue for unit events (damage, etc.)
    public IReadOnlyCollection<UiEvent> Notifications => _notifications.ToArray();
    public IReadOnlyList<UiEvent> Events => _events;

    /// <summary>
    /// Adds an event to the unit's events queue
    /// </summary>
    /// <param name="uiEvent">The event to add</param>
    public void AddEvent(UiEvent uiEvent)
    {
        _notifications.Enqueue(uiEvent);
        _events.Add(uiEvent);
    }
    
    /// <summary>
    /// Dequeues and returns the next event from the unit's events queue
    /// </summary>
    /// <returns>The next event, or null if the queue is empty</returns>
    public UiEvent? DequeueNotification()
    {
        return _notifications.Count > 0 ? _notifications.Dequeue() : null;
    }
    
    /// <summary>
    /// Clears all events from the unit's events queue
    /// </summary>
    public void ClearEvents()
    {
        _notifications.Clear();
        _events.Clear();
    }

    /// <summary>
    /// Adds movement points to the unit's movement points spent
    /// </summary>
    /// <param name="points">The number of points to add</param>
    protected void SpendMovementPoints(int points)
    {
        MovementPointsSpent += points;
    }
}