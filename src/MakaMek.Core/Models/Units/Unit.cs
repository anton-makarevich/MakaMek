using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
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
    
    public UnitStatus Status { get; protected set; }

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
    
    // Movement capabilities
    public virtual int GetMovementPoints(MovementType type)
    {
        return type switch
        {
            MovementType.Walk => BaseMovement,
            MovementType.Run => (int)Math.Ceiling(BaseMovement * 1.5),
            MovementType.Jump => GetAllComponents<JumpJets>().Sum(j => j.JumpMp),
            MovementType.Sprint => BaseMovement * 2,
            MovementType.Masc => HasAvailableComponent<Masc>() ? BaseMovement * 2 : (int)(BaseMovement * 1.5),
            _ => 0
        };
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
    public int HeatDissipation => GetAllComponents<HeatSink>().Sum(hs => hs.HeatDissipation)
                                  +10; // Engine heat sinks
    
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
        var heatSinks = GetAllComponents<HeatSink>().Count();
        var engineHeatSinks = 10; // Always 10 engine heat sinks
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
            DissipationData = dissipationData
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
    public IPilot? Crew { get; protected set; }

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
        if (Status is UnitStatus.Shutdown or UnitStatus.PoweredDown)
        {
            Status = UnitStatus.Active;
        }
    }

    public virtual void Shutdown()
    {
        if (Status == UnitStatus.Active)
        {
            Status = UnitStatus.Shutdown;
        }
    }
    
    public void ApplyDamage(List<HitLocationData> hitLocations)
    {
        foreach (var hitLocation in hitLocations)
        {
            var targetPart = _parts.Find(p => p.Location == hitLocation.Location);
            if (targetPart != null)
            {
                ApplyDamage(hitLocation.Damage, targetPart);
            }
        }
    }

    internal virtual void ApplyDamage(int damage, UnitPart targetPart)
    {
        var remainingDamage = targetPart.ApplyDamage(damage);
        
        // If there's remaining damage, transfer to the connected part
        if (remainingDamage <= 0) return;
        var transferLocation = GetTransferLocation(targetPart.Location);
        if (!transferLocation.HasValue) return;
        var transferPart = _parts.Find(p => p.Location == transferLocation.Value);
        if (transferPart != null)
        {
            ApplyDamage(remainingDamage, transferPart);
        }
    }

    // Different unit types will have different damage transfer patterns
    protected abstract PartLocation? GetTransferLocation(PartLocation location);

    public IEnumerable<T> GetAllComponents<T>() where T : Component
    {
        return Parts.SelectMany(p => p.GetComponents<T>());
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
            .Where(a => a.Type == weapon.AmmoType && a.IsAvailable);
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
        MovementPointsSpent = movementPath.Sum(s=>s.Cost);
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
}