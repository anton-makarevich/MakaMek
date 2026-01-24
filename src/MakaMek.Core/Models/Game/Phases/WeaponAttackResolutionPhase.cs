using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Game.Commands;
using Sanet.MakaMek.Core.Data.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class WeaponAttackResolutionPhase(ServerGame game) : GamePhase(game)
{
    private int _currentPlayerIndex;
    private int _currentUnitIndex;
    private int _currentWeaponIndex;
    
    // List of players in initiative order for attack resolution
    private List<IPlayer> _playersInOrder = [];
    
    // Units with weapons that have targets, organized by player
    private readonly Dictionary<Guid, List<IUnit>> _unitsWithTargets = new();
    
    // Dictionary to track accumulated damage data for PSR calculations at the phase end
    private readonly Dictionary<Guid, UnitPhaseAccumulatedDamage> _accumulatedDamageData = new();

    public override void Enter()
    {
        // Clear any accumulated damage data from previous phases
        _accumulatedDamageData.Clear();
        
        // Initialize the attack resolution process
        _playersInOrder = Game.InitiativeOrder.ToList();
        _currentPlayerIndex = 0;
        _currentUnitIndex = 0;
        _currentWeaponIndex = 0;
        
        // Prepare the dictionary of units with targets for each player
        PrepareUnitsWithTargets();
        
        // Start resolving attacks
        ResolveNextAttack();
    }
    
    private void PrepareUnitsWithTargets()
    {
        _unitsWithTargets.Clear();

        foreach (var player in _playersInOrder)
        {
            var unitsWithWeaponTargets = player.Units
                .Where(unit => unit is { CanFireWeapons: true, HasDeclaredWeaponAttack: true })
                .ToList();

            if (unitsWithWeaponTargets.Count > 0)
            {
                _unitsWithTargets[player.Id] = unitsWithWeaponTargets;
            }
        }
    }

    private void ResolveNextAttack()
    {
        // Check if we've processed all players
        if (_currentPlayerIndex >= _playersInOrder.Count)
        {
            // Calculate PSRs for all units that accumulated damage during this phase
            CalculateEndOfPhasePsrs();
            
            Game.TransitionToNextPhase(Name);
            return;
        }

        var currentPlayer = _playersInOrder[_currentPlayerIndex];

        // Skip players with no units that have targets
        if (!_unitsWithTargets.TryGetValue(currentPlayer.Id, out var unitsWithTargets) || unitsWithTargets.Count == 0)
        {
            MoveToNextPlayer();
            ResolveNextAttack();
            return;
        }

        // Check if we've processed all units for the current player
        if (_currentUnitIndex >= unitsWithTargets.Count)
        {
            MoveToNextPlayer();
            ResolveNextAttack();
            return;
        }

        var currentUnit = unitsWithTargets[_currentUnitIndex];
        var weaponTargets = currentUnit.DeclaredWeaponTargets??[];

        // Check if we've processed all weapon targets for the current unit
        if (_currentWeaponIndex >= weaponTargets.Count)
        {
            MoveToNextUnit();
            ResolveNextAttack();
            return;
        }

        var currentWeaponTarget = weaponTargets[_currentWeaponIndex];
        
        // Find the weapon and target unit
        var primaryAssignment = currentWeaponTarget.Weapon.Assignments.FirstOrDefault();
        var currentWeapon = primaryAssignment != null ?
            currentUnit.GetMountedComponentAtLocation<Weapon>(primaryAssignment.Location, primaryAssignment.FirstSlot) :
            null;
        
        // Take all units not just alive as we should resolve attack even if the unit is already destroyed
        var allUnits = Game.Players.SelectMany(p => p.Units); 
        var targetUnit = allUnits.FirstOrDefault(u => u.Id == currentWeaponTarget.TargetId);

        if (currentWeapon != null && targetUnit != null)
        {
            var resolution = ResolveAttack(currentUnit, targetUnit, currentWeapon, currentWeaponTarget);
            FinalizeAttackResolution(currentPlayer, currentUnit, currentWeapon, targetUnit, resolution);
        }

        // Move to the next weapon
        _currentWeaponIndex++;

        // Continue resolving attacks
        ResolveNextAttack();
    }

    private AttackResolutionData ResolveAttack(IUnit attacker, IUnit target, Weapon weapon, WeaponTargetData weaponTargetData)
    {

        if (Game.BattleMap == null)
        {
            throw new Exception("Battle map is null");
        }
        
        // Calculate to-hit number, including aimed shot modifiers if applicable
        var toHitNumber = Game.ToHitCalculator.GetToHitNumber(
            attacker,
            target,
            weapon,
            Game.BattleMap,
            weaponTargetData.IsPrimaryTarget,
            weaponTargetData.AimedShotTarget);
        
        // Roll 2D6 for attack
        var attackRoll = Game.DiceRoller.Roll2D6();
        var totalRoll = attackRoll.Sum(d => d.Result);

        var isHit = totalRoll >= toHitNumber;

        // Determine an attack direction (will be null if not a hit)
        var attackDirection = HitDirection.Front;

        // If hit, determine location and damage
        AttackHitLocationsData? hitLocationsData = null;

        if (!isHit) return new AttackResolutionData(toHitNumber,
            attackRoll,
            isHit,
            attackDirection,
            weapon.ExternalHeat,
            hitLocationsData);
        // Determine an attack direction once for this weapon attack
        attackDirection = DetermineAttackDirection(attacker, target);

        // Check if it's a cluster weapon
        if (weapon.WeaponSize > 1)
        {
            // It's a cluster weapon, handle multiple hits
            hitLocationsData = ResolveClusterWeaponHit(weapon, target, attackDirection, weaponTargetData);

            // Create hit locations data with multiple hits
            return new AttackResolutionData(toHitNumber,
                attackRoll,
                isHit,
                attackDirection,
                weapon.ExternalHeat,
                hitLocationsData);
        }

        // Standard weapon, single hit location
        var hitLocationData = DetermineHitLocation(attackDirection, weapon.Damage, target, weapon, weaponTargetData);

        // Create hit locations data with a single hit
        hitLocationsData = new AttackHitLocationsData(
            [hitLocationData],
            weapon.Damage,
            [], // No cluster roll for standard weapons
            1 // Single hit
        );

        return new AttackResolutionData(toHitNumber,
            attackRoll,
            isHit,
            attackDirection,
            weapon.ExternalHeat,
            hitLocationsData);
    }

    private AttackHitLocationsData ResolveClusterWeaponHit(Weapon weapon,
        IUnit target,
        HitDirection attackDirection,
        WeaponTargetData weaponTargetData)
    {
        // Roll for cluster hits
        var clusterRoll = Game.DiceRoller.Roll2D6();
        var clusterRollTotal = clusterRoll.Sum(d => d.Result);
        
        // Determine how many missiles hit using the cluster hit table
        var missilesHit = Game.RulesProvider.GetClusterHits(clusterRollTotal, weapon.WeaponSize);
        
        // Calculate damage per missile
        var damagePerMissile = weapon.Damage / weapon.WeaponSize;
        
        // Calculate how many complete clusters hit and if there's a partial cluster
        var completeClusterHits = missilesHit / weapon.ClusterSize;
        var remainingMissiles = missilesHit % weapon.ClusterSize;
        
        var hitLocations = new List<LocationHitData>();
        var totalDamage = 0;

        // For each complete cluster that hit
        for (var i = 0; i < completeClusterHits; i++)
        {
            // Calculate damage for this cluster
            var clusterDamage = weapon.ClusterSize * damagePerMissile;

            // Determine the hit location for this cluster
            // Pass accumulated hit locations so damage calculation considers previous clusters
            var hitLocationData = DetermineHitLocation(attackDirection,
                clusterDamage,
                target,
                weapon,
                weaponTargetData,
                hitLocations);

            // Add to hit locations and update total damage
            hitLocations.Add(hitLocationData);
            totalDamage += clusterDamage;
        }

        // If there are remaining missiles (partial cluster)
        if (remainingMissiles > 0)
        {
            // Calculate damage for the partial cluster
            var partialClusterDamage = remainingMissiles * damagePerMissile;

            // Determine the hit location for the partial cluster
            // Pass accumulated hit locations so damage calculation considers previous clusters
            var hitLocationData = DetermineHitLocation(
                attackDirection,
                partialClusterDamage,
                target,
                weapon,
                weaponTargetData,
                hitLocations);

            // Add to hit locations and update total damage
            hitLocations.Add(hitLocationData);
            totalDamage += partialClusterDamage;
        }

        return new AttackHitLocationsData(hitLocations, totalDamage, clusterRoll, missilesHit);
    }

    /// <summary>
    /// Determines the hit location for an attack
    /// </summary>
    /// <param name="attackDirection">The direction of the attack</param>
    /// <param name="damage">The damage to be applied to this location</param>
    /// <param name="target">The target unit</param>
    /// <param name="weapon">The firing weapon</param>
    /// <param name="weaponTargetData">Weapon's target data</param>
    /// <param name="accumulatedHitLocations">Optional list of previously resolved hit locations from earlier clusters</param>
    /// <returns>Hit location data with location, damage and dice roll</returns>
    private LocationHitData DetermineHitLocation(
        HitDirection attackDirection,
        int damage,
        IUnit target,
        Weapon weapon,
        WeaponTargetData weaponTargetData,
        IReadOnlyList<LocationHitData>? accumulatedHitLocations = null)
    {
        // If the weapon target data specifies a specific location, use that
        PartLocation? aimedShotLocation = null;
        int[] aimedShotRollResult = [];
        if (IsAimedShotPossible(target, weapon, weaponTargetData))
        {
            aimedShotRollResult = Game.DiceRoller.Roll2D6().Select(d => d.Result).ToArray();
            var aimedShotRoll = aimedShotRollResult.Sum();
            var successValues = Game.RulesProvider.GetAimedShotSuccessValues();
            if (successValues.Contains(aimedShotRoll))
            {
                aimedShotLocation = weaponTargetData.AimedShotTarget;
            }
        }

        int[] locationRoll = [];
        // If the aimed shot location is null, determine the hit location normally
        var hitLocation = aimedShotLocation ?? GetHitLocation(out locationRoll);
        
        // Store the initial location in case we need to transfer
        var initialLocation = hitLocation;
        
        // Check if the location is already destroyed and transfer if needed
        while (target.Parts.TryGetValue(hitLocation, out var part) && part.IsDestroyed)
        {
            var nextLocation = part.GetNextTransferLocation();
            if (nextLocation == null || nextLocation == hitLocation)
                break;

            hitLocation = nextLocation.Value;
        }
        
        // Use DamageTransferCalculator to calculate damage distribution
        // Pass accumulated hit locations so the calculator can apply previous cluster damage
        var damageData = Game.DamageTransferCalculator.CalculateStructureDamage(
            target, hitLocation, damage, attackDirection, accumulatedHitLocations);

        return new LocationHitData(
            damageData,
            aimedShotRollResult,
            locationRoll,
            initialLocation);

        bool IsAimedShotPossible(IUnit unit, Weapon weapon1, WeaponTargetData weaponTargetData1)
        {
            return unit.IsImmobile // Immobile target
                   && weapon1.IsAimShotCapable // Aimed shot capable weapon
                   && weaponTargetData1.AimedShotTarget.HasValue; // Aimed shot target specified
        }

        // Get hit location based on the roll and attack direction
        PartLocation GetHitLocation(out int[] innerLocationRoll)
        {
            // Roll for hit location
            innerLocationRoll = Game.DiceRoller.Roll2D6().Select(d => d.Result).ToArray();
            var locationRollTotal = innerLocationRoll.Sum();
            return Game.RulesProvider.GetHitLocation(locationRollTotal, attackDirection);
        }
    }

    /// <summary>
    /// Determines the direction from which the attack is coming
    /// </summary>
    /// <param name="attacker">The attacking unit</param>
    /// <param name="target">The target unit</param>
    /// <returns>The direction from which the attack is coming</returns>
    private HitDirection DetermineAttackDirection(IUnit? attacker, IUnit target)
    {
        // Default to forward if no attacker is provided or positions are missing
        if (attacker?.Position == null || target.Position == null)
            return HitDirection.Front;
            
        // Check each firing arc to determine which one contains the attacker
        // TODO this is a temporary simplified approach, actually attack direction and firing arc are different things
        foreach (var arc in Enum.GetValues<FiringArc>())
        {
            if (target.Position.Coordinates.IsInFiringArc(attacker.Position.Coordinates, target.Position.Facing, arc))
            {
                return arc switch
                {
                    FiringArc.Left => HitDirection.Left,
                    FiringArc.Right => HitDirection.Right,
                    FiringArc.Rear => HitDirection.Rear,
                    _ => HitDirection.Front
                };
            }
        }
        
        // Default to forward if no arc is determined
        return HitDirection.Front;
    }

    private void FinalizeAttackResolution(IPlayer player, IUnit attacker, Weapon weapon, IUnit target,
        AttackResolutionData resolution)
    {
        // Track destroyed parts before damage
        var destroyedPartsBefore = target.Parts.Values
            .Where(p => p.IsDestroyed).Select(p => p.Location).ToList();
        var wasDestroyedBefore = target.IsDestroyed;

        // Apply damage to the target
        if (resolution is { IsHit: true, HitLocationsData.HitLocations: not null })
        {
            target.ApplyDamage(resolution.HitLocationsData.HitLocations, resolution.AttackDirection);

            // Apply external heat if the weapon has ExternalHeat property
            if (weapon.ExternalHeat > 0)
            {
                target.AddExternalHeat(weapon.Name, weapon.ExternalHeat);
            }
        }

        // Check which parts are newly destroyed
        var destroyedPartsAfter = target.Parts.Values.Where(p => p.IsDestroyed).Select(p => p.Location).ToList();
        var newlyDestroyedParts = destroyedPartsAfter.Except(destroyedPartsBefore).ToList();

        // Check if the unit was destroyed by this attack
        var unitNewlyDestroyed = !wasDestroyedBefore && target.IsDestroyed;

        // Update the resolution data with destruction information
        resolution = resolution with
        {
            DestroyedParts = newlyDestroyedParts.Count != 0 ? newlyDestroyedParts : null,
            UnitDestroyed = unitNewlyDestroyed
        };

        // Create and publish a command to inform clients about the attack resolution
        var command = new WeaponAttackResolutionCommand
        {
            GameOriginId = Game.Id,
            PlayerId = player.Id,
            AttackerId = attacker.Id,
            WeaponData = weapon.ToData(),
            TargetId = target.Id,
            ResolutionData = resolution
        };

        attacker.FireWeapon(command.WeaponData);

        Game.CommandPublisher.PublishCommand(command);

        IEnumerable<ComponentHitData> allComponentHits = [];
        List<PartLocation> blownOffParts = [];
        // Calculate and send critical hits if any location received structure damage
        if (resolution.HitLocationsData?.HitLocations != null
            && resolution.HitLocationsData.HitLocations
                .Any(h => h.Damage.Any(d => d.StructureDamage > 0)))
        {
            var criticalHitsCommand = Game.CriticalHitsCalculator
                .CalculateAndApplyCriticalHits(target,
                    (resolution.HitLocationsData?.HitLocations!) //nullability is checked above
                    .SelectMany(h => h.Damage).ToList());

            
            if (criticalHitsCommand != null)
            {
                criticalHitsCommand.GameOriginId = Game.Id;
                Game.CommandPublisher.PublishCommand(criticalHitsCommand);

                // Check for component hits that can cause a fall
                allComponentHits = criticalHitsCommand.CriticalHits.SelectMany(ch => ch.HitComponents ?? []);

                // Also track parts blown off by critical hits (e.g., leg/arm/head)
                blownOffParts = criticalHitsCommand.CriticalHits
                    .Where(ch => ch.IsBlownOff)
                    .Select(ch => ch.Location).ToList();
            }
        }

        // Process consciousness rolls for pilot damage from the attack and critical hits
        // This must happen AFTER both WeaponAttackResolutionCommand and CriticalHitsResolutionCommand are published
        // to maintain correct chronological order in the game log
        ProcessConsciousnessRollsForUnit(target);

        var allDestroyedParts = (resolution.DestroyedParts ?? [])
            .Concat(blownOffParts).Distinct();

        // Add component hits to accumulated damage data
        if (!_accumulatedDamageData.TryGetValue(target.Id, out var accumulatedDamage))
        {
            accumulatedDamage = new UnitPhaseAccumulatedDamage();
            _accumulatedDamageData[target.Id] = accumulatedDamage;
        }

        accumulatedDamage.AllComponentHits.AddRange(allComponentHits);
        foreach (var part in allDestroyedParts)
            accumulatedDamage.AllDestroyedParts.Add(part);
    }

    private void MoveToNextUnit()
    {
        _currentUnitIndex++;
        _currentWeaponIndex = 0;
    }
    
    private void MoveToNextPlayer()
    {
        _currentPlayerIndex++;
        _currentUnitIndex = 0;
        _currentWeaponIndex = 0;
    }

    public override void HandleCommand(IGameCommand command)
    {
        // This phase doesn't process incoming commands, but we need to implement this method
    }

    public override PhaseNames Name => PhaseNames.WeaponAttackResolution;

    /// <summary>
    /// Calculates PSRs for all units that accumulated damage during the weapon attack resolution phase
    /// This is called at the end of the phase to comply with BattleTech rules
    /// </summary>
    private void CalculateEndOfPhasePsrs()
    {
        foreach (var (unitId, accumulatedDamage) in _accumulatedDamageData)
        {
            // Find the unit
            var unit = Game.Players
                .SelectMany(p => p.Units)
                .FirstOrDefault(u => u.Id == unitId);
                
            if (unit is not Mech targetMech) continue;
            
            // Calculate PSRs using the accumulated damage data from the entire phase
            var mechFallingCommands = Game.FallProcessor.ProcessPotentialFall(
                targetMech,
                Game,
                accumulatedDamage.AllComponentHits,
                accumulatedDamage.AllDestroyedParts);

            foreach (var fallingCommand in mechFallingCommands)
            {
                Game.OnMechFalling(fallingCommand);
                Game.CommandPublisher.PublishCommand(fallingCommand);
                if (fallingCommand.DamageData is null) break;

                var locationsWithDamagedStructure = fallingCommand.DamageData.HitLocations.HitLocations
                    .Where(h => h.Damage.Any(d => d.StructureDamage > 0))
                    .SelectMany(h => h.Damage)
                    .ToList();
                if (locationsWithDamagedStructure.Count != 0)
                {
                    var fallCriticalHitsCommand = Game.CriticalHitsCalculator
                        .CalculateAndApplyCriticalHits(targetMech, locationsWithDamagedStructure);
                    if (fallCriticalHitsCommand != null)
                    {
                        fallCriticalHitsCommand.GameOriginId = Game.Id;
                        Game.CommandPublisher.PublishCommand(fallCriticalHitsCommand);
                    }
                }
                // Process consciousness rolls for pilot damage accumulated during this phase
                ProcessConsciousnessRollsForUnit(targetMech);
                break;
            }
        }
        
        // Clear the accumulated damage data after processing
        _accumulatedDamageData.Clear();
    }

    /// <summary>
    /// Tracks accumulated damage data for a unit during the weapon attack resolution phase
    /// Used to calculate PSRs at the end of the phase instead of after each attack
    /// </summary>
    private record UnitPhaseAccumulatedDamage
    {
        public List<ComponentHitData> AllComponentHits { get; } = [];
        public List<PartLocation> AllDestroyedParts { get; } = [];
    }
}
