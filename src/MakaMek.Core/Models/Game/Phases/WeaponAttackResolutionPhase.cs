using Sanet.MakaMek.Core.Data.Game;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Models.Game.Commands;
using Sanet.MakaMek.Core.Models.Game.Commands.Server;
using Sanet.MakaMek.Core.Models.Game.Players;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;

namespace Sanet.MakaMek.Core.Models.Game.Phases;

public class WeaponAttackResolutionPhase(ServerGame game) : GamePhase(game)
{
    private int _currentPlayerIndex;
    private int _currentUnitIndex;
    private int _currentWeaponIndex;
    
    // List of players in initiative order for attack resolution
    private List<IPlayer> _playersInOrder = [];
    
    // Units with weapons that have targets, organized by player
    private readonly Dictionary<Guid, List<Unit>> _unitsWithTargets = new();

    public override void Enter()
    {
        base.Enter();
        
        // Initialize attack resolution process
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
                .Where(unit => unit.Parts.SelectMany(p=>p.GetComponents<Weapon>()).Any(weapon => weapon.Target != null))
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
        var weaponsWithTargets = currentUnit.Parts.SelectMany(p => p.GetComponents<Weapon>())
            .Where(weapon => weapon.Target != null)
            .ToList();

        // Check if we've processed all weapons for the current unit
        if (_currentWeaponIndex >= weaponsWithTargets.Count)
        {
            MoveToNextUnit();
            ResolveNextAttack();
            return;
        }

        var currentWeapon = weaponsWithTargets[_currentWeaponIndex];
        var targetUnit = currentWeapon.Target;

        if (targetUnit != null)
        {
            var resolution = ResolveAttack(currentUnit, targetUnit, currentWeapon);
            PublishAttackResolution(currentPlayer, currentUnit, currentWeapon, targetUnit, resolution);
        }

        // Move to the next weapon
        _currentWeaponIndex++;

        // Continue resolving attacks
        ResolveNextAttack();
    }

    private AttackResolutionData ResolveAttack(Unit attacker, Unit target, Weapon weapon)
    {
        
        if (Game.BattleMap == null)
        {
            throw new Exception("Battle map is null");
        }
        // Calculate to-hit number
        var toHitNumber = Game.ToHitCalculator.GetToHitNumber(
            attacker,
            target,
            weapon,
            Game.BattleMap);

        // Roll 2D6 for attack
        var attackRoll = Game.DiceRoller.Roll2D6();
        var totalRoll = attackRoll.Sum(d => d.Result);

        var isHit = totalRoll >= toHitNumber;

        // Determine attack direction (will be null if not a hit)
        FiringArc? attackDirection = null;

        // If hit, determine location and damage
        AttackHitLocationsData? hitLocationsData = null;

        if (!isHit) return new AttackResolutionData(toHitNumber, attackRoll, isHit, attackDirection, hitLocationsData);
        // Determine an attack direction once for this weapon attack
        attackDirection = DetermineAttackDirection(attacker, target);

        // Check if it's a cluster weapon
        if (weapon.WeaponSize > 1)
        {
            // It's a cluster weapon, handle multiple hits
            hitLocationsData = ResolveClusterWeaponHit(weapon, attackDirection.Value);

            // Create hit locations data with multiple hits
            return new AttackResolutionData(toHitNumber, attackRoll, isHit, attackDirection, hitLocationsData);
        }

        // Standard weapon, single hit location
        var hitLocationData = DetermineHitLocation(attackDirection.Value, weapon.Damage);

        // Create hit locations data with a single hit
        hitLocationsData = new AttackHitLocationsData(
            [hitLocationData],
            weapon.Damage,
            [], // No cluster roll for standard weapons
            1 // Single hit
        );

        return new AttackResolutionData(toHitNumber, attackRoll, isHit, attackDirection, hitLocationsData);
    }

    private AttackHitLocationsData ResolveClusterWeaponHit(Weapon weapon, FiringArc attackDirection)
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
        
        var hitLocations = new List<HitLocationData>();
        var totalDamage = 0;
        
        // For each complete cluster that hit
        for (var i = 0; i < completeClusterHits; i++)
        {
            // Calculate damage for this cluster
            var clusterDamage = weapon.ClusterSize * damagePerMissile;
            
            // Determine hit location for this cluster
            var hitLocationData = DetermineHitLocation(attackDirection, clusterDamage);
            
            // Add to hit locations and update total damage
            hitLocations.Add(hitLocationData);
            totalDamage += clusterDamage;
        }
        
        // If there are remaining missiles (partial cluster)
        if (remainingMissiles <= 0)
            return new AttackHitLocationsData(hitLocations, totalDamage, clusterRoll, missilesHit);
        {
            // Calculate damage for the partial cluster
            var partialClusterDamage = remainingMissiles * damagePerMissile;
            
            // Determine hit location for the partial cluster
            var hitLocationData = DetermineHitLocation(attackDirection, partialClusterDamage);
            
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
    /// <returns>Hit location data with location, damage and dice roll</returns>
    private HitLocationData DetermineHitLocation(FiringArc attackDirection, int damage)
    {
        // Roll for hit location
        var locationRoll = Game.DiceRoller.Roll2D6();
        var locationRollTotal = locationRoll.Sum(d => d.Result);
        
        // Get hit location based on roll and attack direction
        var hitLocation = Game.RulesProvider.GetHitLocation(locationRollTotal, attackDirection);
        
        // Return hit location data
        return new HitLocationData(hitLocation, damage, locationRoll);
    }
    
    /// <summary>
    /// Determines the direction from which the attack is coming
    /// </summary>
    /// <param name="attacker">The attacking unit</param>
    /// <param name="target">The target unit</param>
    /// <returns>The firing arc that contains the attacker</returns>
    private FiringArc DetermineAttackDirection(Unit? attacker, Unit target)
    {
        // Default to forward if no attacker is provided or positions are missing
        if (attacker == null || attacker.Position == null || target.Position == null)
            return FiringArc.Forward;
            
        // Check each firing arc to determine which one contains the attacker
        foreach (var arc in Enum.GetValues<FiringArc>())
        {
            if (target.Position.Coordinates.IsInFiringArc(attacker.Position.Coordinates, target.Position.Facing, arc))
            {
                return arc;
            }
        }
        
        // Default to forward if no arc is determined
        return FiringArc.Forward;
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
    
    private void PublishAttackResolution(IPlayer player, Unit attacker, Weapon weapon, Unit target, AttackResolutionData resolution)
    {
        // Create and publish a command to inform clients about the attack resolution
        var command = new WeaponAttackResolutionCommand
        {
            GameOriginId = Game.Id,
            PlayerId = player.Id,
            AttackerId = attacker.Id,
            WeaponData = new WeaponData
            {
                Location = weapon.MountedOn!.Location,
                Name = weapon.Name,
                Slots = weapon.MountedAtSlots
            },
            TargetId = target.Id,
            ResolutionData = resolution
        };
        
        attacker.FireWeapon(command.WeaponData);
        if (command.ResolutionData.IsHit && command.ResolutionData.HitLocationsData?.HitLocations!=null)
        {
            target.ApplyDamage(command.ResolutionData.HitLocationsData.HitLocations);
        }

        Game.CommandPublisher.PublishCommand(command);
    }

    public override void HandleCommand(IGameCommand command)
    {
        // This phase doesn't process incoming commands, but we need to implement this method
    }

    public override PhaseNames Name => PhaseNames.WeaponAttackResolution;
}
