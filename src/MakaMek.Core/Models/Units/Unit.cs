using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Events;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.HeatPenalties;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Penalties.MovementPenalties;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units.Components;
using Sanet.MakaMek.Core.Models.Units.Components.Engines;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Pilots;

namespace Sanet.MakaMek.Core.Models.Units;

public abstract class Unit : IUnit
{
    protected readonly Dictionary<PartLocation, UnitPart> _parts;
    private readonly Queue<UiEvent> _notifications = new();
    private readonly List<UiEvent> _events = [];

    protected Unit(string chassis, string model, int tonnage,
        IEnumerable<UnitPart> parts,
        Guid? id = null)
    {
        Chassis = chassis;
        Model = model;
        Name = $"{chassis} {model}";
        Tonnage = tonnage;
        _parts = parts.ToDictionary(p => p.Location, p => p);
        // Set the Unit reference for each part
        foreach (var part in _parts.Values)
        {
            part.Unit = this;
        }

        if (id.HasValue)
        {
            Id = id.Value;
        }

        WeaponAttackState = new UnitWeaponAttackState();
    }

    public string Chassis { get; }
    public string Model { get; }
    public string Name { get; }
    public int Tonnage { get; }

    public IPlayer? Owner { get; internal set; }

    /// <summary>
    /// Gets the weapon attack state for this unit, tracking weapon selections and targeting
    /// </summary>
    public UnitWeaponAttackState WeaponAttackState { get; }

    private UnitStatus _status;

    public UnitStatus Status
    {
        get
        {
            if ((_status & UnitStatus.Destroyed) == UnitStatus.Destroyed)
            {
                return UnitStatus.Destroyed;
            }
            if (IsImmobile) return _status | UnitStatus.Immobile;
            return _status;
        }
        protected set
        {
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
    /// Data about the current shutdown state, if any
    /// </summary>
    public ShutdownData? CurrentShutdownData { get; private set; }

    /// <summary>
    /// Gets whether the unit is shutdown. Returns true if shutdown data is present.
    /// </summary>
    public bool IsShutdown => CurrentShutdownData.HasValue;

    /// <summary>
    /// Gets whether the unit is immobile.
    /// </summary>
    public virtual bool IsImmobile => false;

    public bool IsOutOfCommission => IsDestroyed || Pilot?.IsDead == true;

    public WeightClass Class => Tonnage.ToWeightClass();

    // Base movement (walking)
    protected int BaseMovement
    {
        get
        {
            if (Tonnage <= 0) return 0;
            var engine = GetComponentsAtLocation<Engine>(PartLocation.CenterTorso).FirstOrDefault();
            if (engine == null) return 0;
            return engine.Rating / Tonnage;
        }
    }
    
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
    
    // Could be moved to ViewModel as those are presentation only
    public int AvailableWalkingPoints => GetMovementPoints(MovementType.Walk);
    public int AvailableRunningPoints => GetMovementPoints(MovementType.Run);
    public int AvailableJumpingPoints => GetMovementPoints(MovementType.Jump);

    /// <summary>
    /// Determines if the unit can move backward with the given movement type
    /// </summary>
    public abstract bool CanMoveBackward(MovementType type);

    /// <summary>
    /// Determines if the unit is in a minimum movement situation (1 MP available)
    /// </summary>
    public virtual bool IsMinimumMovement => false;

    // Location and facing
    public virtual HexPosition? Position { get; protected set; }
    public virtual HexDirection? Facing => Position?.Facing;

    public bool IsDeployed => Position != null;

    public void Deploy(HexPosition position)
    {
        if (Position != null)
        {
            throw new InvalidOperationException($"{Name} is already deployed.");
        }
        Position = position;
    }
    
    /// <summary>
    /// Removes the unit from the board
    /// </summary>
    public void RemoveFromBoard()
    {
        Position = null;
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
        var weaponTargets = GetAllWeaponTargetsData();

        foreach (var weaponTarget in weaponTargets)
        {
            var primaryAssignment = weaponTarget.Weapon.Assignments.FirstOrDefault();
            var weapon = primaryAssignment != null ?
                GetMountedComponentAtLocation<Weapon>(primaryAssignment.Location, primaryAssignment.FirstSlot) :
                null;
            if (weapon == null) continue; // should it be available also?

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

        // Get external heat sources (from weapons like Flamers)
        var externalHeatSources = new List<ExternalHeatData>(_turnExternalHeat);

        return new HeatData
        {
            ExternalHeatCap = rulesProvider.GetExternalHeatCap(),
            MovementHeatSources = movementHeatSources,
            WeaponHeatSources = weaponHeatSources,
            ExternalHeatSources = externalHeatSources,
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
        _turnExternalHeat.Clear();
    }

    protected abstract void ApplyHeatEffects();
    
    // Parts management
    public IReadOnlyDictionary<PartLocation, UnitPart> Parts => _parts;
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
    public int TotalMaxArmor => _parts.Values.Sum(p => p.MaxArmor);
    public int TotalCurrentArmor => _parts.Values.Sum(p => p.CurrentArmor);
    public int TotalMaxStructure => _parts.Values.Sum(p => p.MaxStructure);
    public int TotalCurrentStructure => _parts.Values.Sum(p => p.CurrentStructure);

    // Movement tracking
    public int MovementPointsSpent { get; private set; }
    public MovementType? MovementTypeUsed { get; private set; }
    public int DistanceCovered { get; private set; }

    public bool HasMoved => MovementTypeUsed.HasValue;

    // Damage tracking
    public int TotalPhaseDamage { get; private set; }

    /// <summary>
    /// Tracks external heat applied to this unit during the current phase (e.g., from Flamers)
    /// </summary>
    private readonly List<ExternalHeatData> _turnExternalHeat = [];

    /// <summary>
    /// Indicates whether this unit has declared weapon attacks for the current turn
    /// </summary>
    public bool HasDeclaredWeaponAttack { get; private set; }

    /// <summary>
    /// Indicates whether this unit has applied heat for the current turn
    /// </summary>
    public bool HasAppliedHeat { get; protected set; }

    /// <summary>
    /// Collection of weapon targeting data for the current attack declaration
    /// </summary>
    private readonly List<WeaponTargetData> _weaponTargets = [];

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

    /// <summary>
    /// Adds external heat to this unit from a weapon attack
    /// </summary>
    /// <param name="weaponName">Name of the weapon applying external heat</param>
    /// <param name="heatPoints">Amount of external heat to apply</param>
    public void AddExternalHeat(string weaponName, int heatPoints)
    {
        if (heatPoints <= 0) return;

        _turnExternalHeat.Add(new ExternalHeatData
        {
            WeaponName = weaponName,
            HeatPoints = heatPoints
        });
    }
    
    private void ResetWeaponsTargets()
    {
        _weaponTargets.Clear();
        HasDeclaredWeaponAttack = false;
    }

    /// <summary>
    /// Declares weapon attacks against target units
    /// </summary>
    /// <param name="weaponTargets">The weapon target data containing weapon locations, slots and target IDs</param>
    public void DeclareWeaponAttack(List<WeaponTargetData> weaponTargets)
    {
        if (!IsDeployed)
        {
            throw new InvalidOperationException("Unit is not deployed.");
        }
        
        // Validate and store weapon targets
        _weaponTargets.Clear();
        _weaponTargets.AddRange(weaponTargets);
        
        HasDeclaredWeaponAttack = true;
    }

    /// <summary>
    /// Gets all weapon targeting data for this unit
    /// </summary>
    /// <returns>Read-only collection of weapon target data</returns>
    public IReadOnlyList<WeaponTargetData> GetAllWeaponTargetsData()
    {
        return _weaponTargets.AsReadOnly();
    }

    /// <summary>
    /// Gets weapon targeting data for a specific weapon
    /// </summary>
    /// <param name="weaponLocation">The location of the weapon</param>
    /// <param name="weaponSlots">The slots where the weapon is mounted</param>
    /// <returns>The weapon target data if found, null otherwise</returns>
    public WeaponTargetData? GetWeaponTargetData(PartLocation weaponLocation, int[] weaponSlots)
    {
        return _weaponTargets.FirstOrDefault(wt =>
        {
            var primaryAssignment = wt.Weapon.Assignments.FirstOrDefault();
            return primaryAssignment != null &&
                   primaryAssignment.Location == weaponLocation &&
                   primaryAssignment.GetSlots().OrderBy(s => s).SequenceEqual(weaponSlots.OrderBy(s => s));
        });
    }

    // Methods
    public abstract int CalculateBattleValue();
    
    // Status management
    public virtual void Startup()
    {
        if (!IsShutdown) return;
        CurrentShutdownData = null;
        _status &= ~UnitStatus.Shutdown;
        _status |= UnitStatus.Active;
    }

    /// <summary>
    /// Determines if this unit can fire weapons. Override in derived classes for specific rules.
    /// </summary>
    public virtual bool CanFireWeapons => !IsImmobile && !IsDestroyed;
    
    /// <summary>
    /// Shuts down the unit with specific shutdown data
    /// </summary>
    /// <param name="shutdownData">Information about the shutdown event</param>
    public virtual void Shutdown(ShutdownData shutdownData)
    {
        if (!IsActive) return;
        CurrentShutdownData = shutdownData;
        _status &= ~UnitStatus.Active;
        _status |= UnitStatus.Shutdown;
    }

    /// <summary>
    /// Applies pre-calculated damage to hit locations. Critical hits should be applied separately via ApplyCriticalHits.
    /// </summary>
    /// <param name="hitLocations">Hit locations with pre-calculated armor and structure damage</param>
    /// <param name="hitDirection">Direction of the hit for armor calculations</param>
    public void ApplyDamage(List<LocationHitData> hitLocations, HitDirection hitDirection)
    {
        foreach (var hitLocation in hitLocations)
        {
            foreach (var locationDamage in hitLocation.Damage)
            {
                if (!_parts.TryGetValue(locationDamage.Location, out var targetPart)) continue;
    
                // Apply pre-calculated armor and structure damage
                var totalDamage = locationDamage.ArmorDamage + locationDamage.StructureDamage;
    
                // Track total damage for this phase
                TotalPhaseDamage += totalDamage;
    
                // Apply the damage 
                targetPart.ApplyDamage(totalDamage, hitDirection);
            }
            
        }
        
        UpdateDestroyedStatus();

        if (IsDestroyed)
        {
            AddEvent(new UiEvent(UiEventType.UnitDestroyed, Name));
        }
    }

    /// <summary>
    /// Recomputes this unit's destroyed/immobile status from its parts and pilot state.
    /// Call after mutating parts/components outside normal damage flows.
    /// </summary>
    public abstract void UpdateDestroyedStatus();

    /// <summary>
    /// Applies pre-calculated critical hits data to the unit
    /// </summary>
    /// <param name="criticalHitsData">Pre-calculated critical hits data from the server</param>
    public void ApplyCriticalHits(List<LocationCriticalHitsData> criticalHitsData)
    {
        foreach (var locationData in criticalHitsData)
        {
            if (!_parts.TryGetValue(locationData.Location, out var targetPart)) continue;

            // Handle blown-off parts
            if (locationData.IsBlownOff)
            {
                targetPart.BlowOff();
                continue;
            }

            // Apply critical hits to specific components
            if (locationData.HitComponents != null)
            {
                foreach (var componentHit in locationData.HitComponents)
                {
                    targetPart.CriticalHit(componentHit.Slot);
                    if (componentHit.ExplosionDamage <= 0 
                        || componentHit.ExplosionDamageDistribution.Length == 0) continue;
                    // Trigger explosion event
                    AddEvent(new UiEvent(UiEventType.Explosion, targetPart.Name));
                    // Apply explosion damage if present and enabled
                    foreach (var explosionDamage in componentHit.ExplosionDamageDistribution)
                    {
                        if (!_parts.TryGetValue(explosionDamage.Location, out var damagedPart) 
                            || explosionDamage.StructureDamage <= 0) continue;

                        // Explosion damage bypasses armor and only affects structure
                        damagedPart.ApplyDamage(explosionDamage.StructureDamage, HitDirection.Front, true);

                        // Track explosion damage in total phase damage
                        TotalPhaseDamage += explosionDamage.StructureDamage;
                    }
                }
            }
        }

        UpdateDestroyedStatus();

        if (IsDestroyed)
        {
            AddEvent(new UiEvent(UiEventType.UnitDestroyed, Name));
        }
    }
    
    // Different unit types will have different damage transfer patterns
    public abstract PartLocation? GetTransferLocation(PartLocation location);

    public IEnumerable<T> GetAllComponents<T>() where T : Component
    {
        return Parts.Values
            .SelectMany(p => p.GetComponents<T>().Where(c => c.IsMounted))
            .Distinct();
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
    /// Determines if the unit has any ammunition-carrying weapons with remaining ammo
    /// </summary>
    public bool HasAmmo => HasAvailableComponent<Ammo>();
    
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
        return _parts.TryGetValue(location, out var part) ? part.Components : [];
    }

    /// <summary>
    /// Gets components of a specific type at a specific location
    /// </summary>
    /// <typeparam name="T">The type of component to find</typeparam>
    /// <param name="location">The location to check</param>
    /// <returns>All components of the specified type at the specified location</returns>
    public IEnumerable<T> GetComponentsAtLocation<T>(PartLocation location) where T : Component
    {
        return _parts.TryGetValue(location, out var part) ? part.GetComponents<T>() : [];
    }
    
    /// <summary>
    /// Gets components of a specific type at a specific location and slot
    /// </summary>
    /// <typeparam name="T">The type of component to find</typeparam>
    /// <param name="location">The location to check</param>
    /// <param name="slot">Any slot where the component is mounted</param>
    /// <returns>Components of the specified type at the specified location containing the given slot</returns>
    public T? GetMountedComponentAtLocation<T>(PartLocation? location, int slot) where T : Component
    {
        if (location == null)
            return null;
        var components = GetComponentsAtLocation<T>(location.Value);

        return components.FirstOrDefault(c =>
           c.MountedAtFirstLocationSlots.Contains(slot));
    }

    public void Move(MovementType movementType, MovementPath movementPath)
    {
        if (Position == null)
        {
            throw new InvalidOperationException("Unit is not deployed.");
        } 
        var position = movementType==MovementType.StandingStill || movementPath.Segments.Count == 0
            ? Position
            : movementPath.Destination;
        DistanceCovered = movementPath.DistanceCovered;
        SpendMovementPoints(movementPath.TotalCost);
        MovementTypeUsed = movementType;
        Position = position; 
    }
    
    /// <summary>
    /// Fires a weapon based on the provided weapon data and consumes ammo if required.
    /// </summary>
    /// <param name="weaponData">Data identifying the weapon to fire</param>
    public void FireWeapon(ComponentData weaponData)
    {
        // Find the weapon using the location and slots from weaponData
        var primaryAssignment = weaponData.Assignments.FirstOrDefault();
        if (primaryAssignment == null) return;

        var weapon = GetMountedComponentAtLocation<Weapon>(
            primaryAssignment.Location,
            primaryAssignment.FirstSlot);

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
    /// <param name="damageTransferCalculator">Damage transfer calculator to calculate explosion damage distribution</param>
    /// <returns>Critical hit data or null if no critical hits</returns>
    public abstract LocationCriticalHitsData? CalculateCriticalHitsData(
        PartLocation location, 
        IDiceRoller diceRoller,
        IDamageTransferCalculator damageTransferCalculator);
    
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