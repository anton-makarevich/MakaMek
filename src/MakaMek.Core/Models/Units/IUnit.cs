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
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Pilots;

namespace Sanet.MakaMek.Core.Models.Units;

public interface IUnit
{
    string Chassis { get; }
    string Model { get; }
    string Name { get; }
    int Tonnage { get; }
    IPlayer? Owner { get; }

    /// <summary>
    /// Gets the weapon attack state for this unit, tracking weapon selections and targeting
    /// </summary>
    UnitWeaponAttackState WeaponAttackState { get; }

    UnitStatus Status { get; }

    /// <summary>
    /// Gets whether the unit is destroyed. Returns true if the Destroyed flag is set, regardless of other flags.
    /// </summary>
    bool IsDestroyed { get; }

    /// <summary>
    /// Gets whether the unit is active. Returns true if the Active flag is set, regardless of other flags.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Data about the current shutdown state, if any
    /// </summary>
    ShutdownData? CurrentShutdownData { get; }

    /// <summary>
    /// Gets whether the unit is shutdown. Returns true if shutdown data is present.
    /// </summary>
    bool IsShutdown { get; }

    /// <summary>
    /// Gets whether the unit is immobile.
    /// </summary>
    bool IsImmobile { get; }

    bool IsOutOfCommission { get; }
    WeightClass Class { get; }
    int DamageReducedMovement { get; }
    HeatMovementPenalty? MovementHeatPenalty { get; }
    HeatRollModifier? AttackHeatPenalty { get; }
    EngineHeatPenalty? EngineHeatPenalty { get; }
    IReadOnlyList<RollModifier> MovementModifiers { get; }
    int AvailableWalkingPoints { get; }
    int AvailableRunningPoints { get; }
    int AvailableJumpingPoints { get; }

    /// <summary>
    /// Determines if the unit is in a minimum movement situation (1 MP available)
    /// </summary>
    bool IsMinimumMovement { get; }

    HexPosition? Position { get; }
    HexDirection? Facing { get; }
    bool IsDeployed { get; }
    int CurrentHeat { get; }
    int HeatDissipation { get; }
    int EngineHeatSinks { get; }
    IReadOnlyDictionary<PartLocation, UnitPart> Parts { get; }
    Guid Id { get; }
    IPilot? Pilot { get; }
    int TotalMaxArmor { get; }
    int TotalCurrentArmor { get; }
    int TotalMaxStructure { get; }
    int TotalCurrentStructure { get; }
    int MovementPointsSpent { get; }
    MovementPath? MovementTaken { get; }
    bool HasMoved { get; }
    int TotalPhaseDamage { get; }

    /// <summary>
    /// Indicates whether this unit has declared weapon attacks for the current turn
    /// </summary>
    bool HasDeclaredWeaponAttack { get; }

    /// <summary>
    /// Indicates whether this unit has applied heat for the current turn
    /// </summary>
    bool HasAppliedHeat { get; }

    /// <summary>
    /// Determines if this unit can fire weapons. Override in derived classes for specific rules.
    /// </summary>
    bool CanFireWeapons { get; }

    IReadOnlyCollection<UiEvent> Notifications { get; }
    IReadOnlyList<UiEvent> Events { get; }

    IReadOnlyList<WeaponConfigurationOptions> GetWeaponsConfigurationOptions(HexPosition? forwardPosition = null);

    /// <summary>
    /// Checks if a weapon configuration has been applied to this unit
    /// </summary>
    /// <param name="config">The configuration to check</param>
    /// <returns>True if the configuration is applied, false otherwise</returns>
    bool IsWeaponConfigurationApplied(WeaponConfiguration config);

    IReadOnlyList<RollModifier> GetAttackModifiers(PartLocation location);
    int GetMovementPoints(MovementType _);

    /// <summary>
    /// Determines if the unit can move backward with the given movement type
    /// </summary>
    bool CanMoveBackward(MovementType type);
    
    IReadOnlyList<MovementType> GetAvailableMovementTypes();

    void Deploy(HexPosition position);

    /// <summary>
    /// Removes the unit from the board
    /// </summary>
    void RemoveFromBoard();

    /// <summary>
    /// Calculates and returns heat data for this unit
    /// </summary>
    /// <returns>A HeatData object containing all heat sources and dissipation information</returns>
    HeatData GetHeatData(IRulesProvider rulesProvider);

    int GetProjectedHeatValue(IRulesProvider rulesProvider);

    void ApplyHeat(HeatData heatData);

    /// <summary>
    /// Assigns a pilot to this unit
    /// </summary>
    /// <param name="pilot">The pilot to assign</param>
    void AssignPilot(IPilot pilot);

    /// <summary>
    /// Unassigns the current pilot from this unit
    /// </summary>
    void UnassignPilot();

    /// <summary>
    /// Resets the turn state for the unit
    /// </summary>
    void ResetTurnState();

    /// <summary>
    /// Resets the phase state for the unit
    /// </summary>
    void ResetPhaseState();

    /// <summary>
    /// Adds external heat to this unit from a weapon attack
    /// </summary>
    /// <param name="weaponName">Name of the weapon applying external heat</param>
    /// <param name="heatPoints">Amount of external heat to apply</param>
    void AddExternalHeat(string weaponName, int heatPoints);

    /// <summary>
    /// Declares weapon attacks against target units
    /// </summary>
    /// <param name="weaponTargets">The weapon target data containing weapon locations, slots, and target IDs</param>
    void DeclareWeaponAttack(List<WeaponTargetData> weaponTargets);

    IReadOnlyList<WeaponTargetData>? DeclaredWeaponTargets { get; }

    void ApplyWeaponConfiguration(WeaponConfiguration config);

    int CalculateBattleValue();
    void Startup();

    /// <summary>
    /// Shuts down the unit with specific shutdown data
    /// </summary>
    /// <param name="shutdownData">Information about the shutdown event</param>
    void Shutdown(ShutdownData shutdownData);

    /// <summary>
    /// Applies pre-calculated damage to hit locations. Critical hits should be applied separately via ApplyCriticalHits.
    /// </summary>
    /// <param name="hitLocations">Hit locations with pre-calculated armor and structure damage</param>
    /// <param name="hitDirection">Direction of the hit for armor calculations</param>
    void ApplyDamage(List<LocationHitData> hitLocations, HitDirection hitDirection);

    /// <summary>
    /// Recomputes this unit's destroyed/immobile status from its parts and pilot state.
    /// Call after mutating parts/components outside normal damage flows.
    /// </summary>
    void UpdateDestroyedStatus();

    /// <summary>
    /// Applies pre-calculated critical hits data to the unit
    /// </summary>
    /// <param name="criticalHitsData">Pre-calculated critical hits data from the server</param>
    void ApplyCriticalHits(List<LocationCriticalHitsData> criticalHitsData);

    PartLocation? GetTransferLocation(PartLocation location);
    IEnumerable<T> GetAllComponents<T>() where T : Component;
    IEnumerable<T> GetAvailableComponents<T>() where T : Component;
    bool HasAvailableComponent<T>() where T : Component;

    /// <summary>
    /// Gets all ammo components compatible with the specified weapon
    /// </summary>
    /// <param name="weapon">The weapon to find ammo for</param>
    /// <returns>A collection of ammo components that can be used by the weapon</returns>
    IEnumerable<Ammo> GetAmmoForWeapon(Weapon weapon);

    /// <summary>
    /// Gets the total number of remaining shots for a specific weapon
    /// </summary>
    /// <param name="weapon">The weapon to check ammo for</param>
    /// <returns>The total number of remaining shots, or -1 if the weapon doesn't require ammo</returns>
    int GetRemainingAmmoShots(Weapon weapon);

    /// <summary>
    /// Gets all components at a specific location
    /// </summary>
    /// <param name="location">The location to check</param>
    /// <returns>All components at the specified location</returns>
    IEnumerable<Component> GetComponentsAtLocation(PartLocation location);

    /// <summary>
    /// Gets components of a specific type at a specific location
    /// </summary>
    /// <typeparam name="T">The type of component to find</typeparam>
    /// <param name="location">The location to check</param>
    /// <returns>All components of the specified type at the specified location</returns>
    IEnumerable<T> GetComponentsAtLocation<T>(PartLocation location) where T : Component;

    /// <summary>
    /// Gets components of a specific type at a specific location and slot
    /// </summary>
    /// <typeparam name="T">The type of component to find</typeparam>
    /// <param name="location">The location to check</param>
    /// <param name="slot">Any slot where the component is mounted</param>
    /// <returns>Components of the specified type at the specified location containing the given slot</returns>
    T? GetMountedComponentAtLocation<T>(PartLocation? location, int slot) where T : Component;

    void Move(MovementPath movementPath);

    /// <summary>
    /// Fires a weapon based on the provided weapon data and consumes ammo if required.
    /// </summary>
    /// <param name="weaponData">Data identifying the weapon to fire</param>
    void FireWeapon(ComponentData weaponData);

    /// <summary>
    /// Calculates critical hit data for a specific location and damage
    /// </summary>
    /// <param name="location">The hit location</param>
    /// <param name="diceRoller">The dice roller to use for critical hit determination</param>
    /// <param name="damageTransferCalculator">Damage transfer calculator to calculate explosion damage distribution</param>
    /// <returns>Critical hit data or null if no critical hits</returns>
    LocationCriticalHitsData? CalculateCriticalHitsData(
        PartLocation location, 
        IDiceRoller diceRoller,
        IDamageTransferCalculator damageTransferCalculator);

    /// <summary>
    /// Adds an event to the unit's events queue
    /// </summary>
    /// <param name="uiEvent">The event to add</param>
    void AddEvent(UiEvent uiEvent);

    /// <summary>
    /// Dequeues and returns the next event from the unit's events queue
    /// </summary>
    /// <returns>The next event, or null if the queue is empty</returns>
    UiEvent? DequeueNotification();

    /// <summary>
    /// Clears all events from the unit's events queue
    /// </summary>
    void ClearEvents();
}