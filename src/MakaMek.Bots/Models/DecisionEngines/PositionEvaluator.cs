using Sanet.MakaMek.Bots.Models.Map;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Evaluates tactical positions for movement decisions based on defensive and offensive potential
/// </summary>
public class PositionEvaluator
{
    private readonly IClientGame _game;
    
    public PositionEvaluator(IClientGame game)
    {
        _game = game;
    }
    
    /// <summary>
    /// Calculates the defensive threat index for a position.
    /// Considers enemy weapons that can target this position and their hit probabilities.
    /// </summary>
    /// <param name="path">The position to evaluate and path to get to it</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <returns>Defensive threat index (lower is better)</returns>
    private double CalculateDefensiveIndex(
        MovementPath path,
        IReadOnlyList<IUnit> enemyUnits)
    {
        if (_game.BattleMap == null)
            return 0;

        double defensiveIndex = 0;
        var position = path.Destination; 
        
        foreach (var enemy in enemyUnits)
        {
            if (enemy.Position == null)
                continue;
            
            var enemyPath = enemy.MovementTaken ?? MovementPath.CreateStandingStillPath(enemy.Position);

            // Check line of sight
            if (!_game.BattleMap.HasLineOfSight(enemy.Position.Coordinates, position.Coordinates))
                continue;

            var range = enemy.Position.Coordinates.DistanceTo(position.Coordinates);

            // Get all enemy weapons
            var weapons = enemy.Parts.Values
                .SelectMany(p => p.GetComponents<Weapon>())
                .Where(w => w.IsAvailable);

            foreach (var weapon in weapons)
            {
                // Check if the weapon can fire at this range
                if (range < weapon.MinimumRange || range > weapon.LongRange)
                    continue;
                
                // Check firing arc
                var isInArc = enemy.Position.Coordinates.IsInWeaponFiringArc(position.Coordinates, weapon);
                if (!isInArc)
                    continue;

                // Calculate hit probability using ToHitCalculator with full accuracy
                // For defensive calculation, we're the target
                var hitProbability = CalculateHitProbability(enemy, enemyPath, path, weapon);

                // Determine which arc of the friendly unit would be hit
                var targetArc = GetFiringArcFromPosition(position, enemy.Position.Coordinates);
                var arcMultiplier = targetArc.GetArcMultiplier();

                // Calculate threat value
                var threatValue = hitProbability * weapon.Damage * arcMultiplier;
                defensiveIndex += threatValue;
            }
        }

        return defensiveIndex;
    }

    /// <summary>
    /// Calculates the offensive potential index for a position.
    /// Considers friendly weapons that can target enemies from this position.
    /// Takes into account the movement type used to reach this position for attacker movement modifiers.
    /// </summary>
    /// <param name="path">The position to evaluate and path to get to it</param>
    /// <param name="friendlyUnit">The friendly unit being evaluated</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <returns>Offensive potential index (higher is better)</returns>
    private double CalculateOffensiveIndex(
        MovementPath path,
        IUnit friendlyUnit,
        IReadOnlyList<IUnit> enemyUnits)
    {
        if (_game.BattleMap == null)
            return 0;
        
        var (hexCoordinates, weaponFacing) = path.Destination;

        double offensiveIndex = 0;

        // Get all friendly weapons
        var weapons = friendlyUnit.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .Where(w => w.IsAvailable)
            .ToList();

        foreach (var enemy in enemyUnits)
        {
            if (enemy.Position == null)
                continue;
            
            var enemyPath = enemy.MovementTaken ?? MovementPath.CreateStandingStillPath(enemy.Position);

            // Check line of sight
            if (!_game.BattleMap.HasLineOfSight(hexCoordinates, enemy.Position.Coordinates))
                continue;

            var distanceToEnemy = hexCoordinates.DistanceTo(enemy.Position.Coordinates);

            foreach (var weapon in weapons)
            {
                // Check if the weapon can fire at this range
                if (distanceToEnemy < weapon.MinimumRange || distanceToEnemy > weapon.LongRange)
                    continue;
                
                // Determine weapon facing from the position (assume only forward facing for now)

                var isInArc = hexCoordinates.IsInWeaponFiringArc(enemy.Position.Coordinates, weapon, weaponFacing);
                if (!isInArc)
                    continue;
                
                // Calculate hit probability with a movement type affecting the attacker movement modifier
                var hitProbability = CalculateHitProbability(friendlyUnit, path, enemyPath, weapon);

                // Determine which arc of the enemy would be hit (bonus for rear/side shots)
                var targetArc = GetFiringArcFromPosition(enemy.Position, hexCoordinates);
                var arcBonus = targetArc.GetArcMultiplier();

                // Calculate damage value
                var damageValue = hitProbability * weapon.Damage * arcBonus;
                offensiveIndex += damageValue;
            }
        }

        return offensiveIndex;
    }

    /// <summary>
    /// Evaluates a single path with a specific movement type and returns its score
    /// </summary>
    /// <param name="friendlyUnit">The friendly unit being evaluated</param>
    /// <param name="path">The movement path to evaluate</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <returns>Position score including the path</returns>
    public PositionScore EvaluatePath(
        IUnit friendlyUnit,
        MovementPath path,
        IReadOnlyList<IUnit> enemyUnits)
    {
        // Extract position and hexesTraveled from the path
        var position = path.Destination;

        var defensiveIndex = CalculateDefensiveIndex(path, enemyUnits);
        var offensiveIndex = CalculateOffensiveIndex(path, friendlyUnit, enemyUnits);

        return new PositionScore
        {
            Position = position,
            MovementType = path.MovementType,
            Path = path,
            DefensiveIndex = defensiveIndex,
            OffensiveIndex = offensiveIndex
        };
    }

    /// <summary>
    /// Calculates hit probability for an enemy attacking the friendly unit at a hypothetical position.
    /// Uses ToHitCalculator with AttackScenario for full accuracy including all modifiers.
    /// </summary>
    private double CalculateHitProbability(
        IUnit attacker,
        MovementPath attackerPath,
        MovementPath targetPath,
        Weapon weapon)
    {
        if (_game.BattleMap == null || attacker.Position == null || attacker.Pilot == null)
            return 0;
        
        // Get weapon location for attack modifiers
        var weaponLocation = weapon.FirstMountPartLocation;
        if (weaponLocation == null)
            return 0;

        var targetPosition = targetPath.Destination;

        // Get current attack modifiers from the attacker (heat, prone, sensors, arm actuators, etc.)
        var attackerModifiers = attacker.GetAttackModifiers(weaponLocation.Value);

        // Determine attacker's movement type (use actual if available, otherwise assume it will walk for now)
        var attackerMovementType = attackerPath.MovementType;

        // Create a hypothetical attack scenario
        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: attacker.Pilot.Gunnery,
            attackerPosition: attacker.Position,
            attackerMovementType: attackerMovementType,
            targetPosition: targetPosition,
            targetHexesMoved: targetPath.HexesTraveled,
            attackerModifiers: attackerModifiers,
            attackerFacing: attackerPath.Destination.Facing
            );

        // Use ToHitCalculator with full accuracy (includes terrain, heat, damage, etc.)
        var toHitNumber = _game.ToHitCalculator.GetToHitNumber(scenario, weapon, _game.BattleMap);

        return DiceUtils.Calculate2d6Probability(toHitNumber);
    }
    
    /// <summary>
    /// Gets the firing arc from which a position would be hit
    /// </summary>
    private FiringArc GetFiringArcFromPosition(HexPosition unitPosition, HexCoordinates attackerPosition)
    {
        if (unitPosition.Coordinates.IsInFiringArc(attackerPosition, unitPosition.Facing, FiringArc.Front))
            return FiringArc.Front;
        if (unitPosition.Coordinates.IsInFiringArc(attackerPosition, unitPosition.Facing, FiringArc.Left))
            return FiringArc.Left;
        if (unitPosition.Coordinates.IsInFiringArc(attackerPosition, unitPosition.Facing, FiringArc.Right))
            return FiringArc.Right;
        
        return FiringArc.Rear;
    }
}
