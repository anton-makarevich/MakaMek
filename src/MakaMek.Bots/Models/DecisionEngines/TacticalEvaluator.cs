using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Models.Map;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Utils;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Evaluates tactical situations for movement and weapon decisions
/// </summary>
public class TacticalEvaluator : ITacticalEvaluator
{
    private readonly IClientGame _game;
    
    public TacticalEvaluator(IClientGame game)
    {
        _game = game;
    }
    
    /// <summary>
    /// Evaluates a single path with a specific movement type and returns its score
    /// </summary>
    /// <param name="unit">The unit being evaluated</param>
    /// <param name="path">The movement path for the unit to evaluate</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <returns>Position score including the path</returns>
    public async Task<PositionScore> EvaluatePath(
        IUnit unit,
        MovementPath path,
        IReadOnlyList<IUnit> enemyUnits)
    {
        var defensiveIndex = CalculateDefensiveIndex(path, enemyUnits);
        var offensiveIndex = (await EvaluateTargets(unit, path, enemyUnits)).Sum(t => t.Score);

        return new PositionScore
        {
            Position = path.Destination,
            MovementType = path.MovementType,
            Path = path,
            DefensiveIndex = defensiveIndex,
            OffensiveIndex = offensiveIndex
        };
    }
    
    /// <summary>
    /// Evaluates potential targets for a unit
    /// </summary>
    public Task<IReadOnlyList<TargetScore>> EvaluateTargets(
        IUnit attacker, MovementPath attackerPath, IReadOnlyList<IUnit> potentialTargets)
    {
        if (_game.BattleMap == null || attacker.Position == null)
            return Task.FromResult<IReadOnlyList<TargetScore>>([]);

        var results = new List<TargetScore>();
        
        // Get all friendly weapons
        var weapons = attacker.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .Where(w => w.IsAvailable)
            .ToList();

        foreach (var target in potentialTargets)
        {
            if (target.Position == null)
                continue;

            var targetPath = target.MovementTaken ?? MovementPath.CreateStandingStillPath(target.Position);
            var viableWeapons = EvaluateWeaponsForTarget(attacker,
                attackerPath,
                targetPath,
                weapons);

            if (viableWeapons.Count <= 0) continue;
            
            // Determine which arc of the enemy would be hit (bonus for rear/side shots)
            var targetArc = GetFiringArcFromPosition(target.Position, attackerPath.Destination.Coordinates);
            var arcBonus = targetArc.GetArcMultiplier();
            var targetScoreValue = viableWeapons.Sum(w => 
                w.HitProbability * w.Weapon.Damage) * arcBonus;
            results.Add(new TargetScore
            {
                TargetId = target.Id,
                Score = targetScoreValue,
                ViableWeapons = viableWeapons
            });
        }

        return Task.FromResult<IReadOnlyList<TargetScore>>(results);
    }
    
    /// <summary>
    /// Calculates the defensive threat index for a position.
    /// Considers enemy weapons that can target this position and their hit probabilities.
    /// </summary>
    /// <param name="defenderPath">The position to evaluate and path to get to it</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <returns>Defensive threat index (lower is better)</returns>
    private double CalculateDefensiveIndex(
        MovementPath defenderPath,
        IReadOnlyList<IUnit> enemyUnits)
    {
        if (_game.BattleMap == null)
            return 0;

        double defensiveIndex = 0;
        var position = defenderPath.Destination; 
        
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
                var hitProbability = CalculateHitProbability(enemy, enemyPath, defenderPath, weapon);

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
    /// Evaluates all weapons against a target and returns a list of viable weapons with hit probabilities
    /// </summary>
    private List<WeaponEvaluationData> EvaluateWeaponsForTarget(
        IUnit attacker,
        MovementPath attackerPath,
        MovementPath targetPath,
        IReadOnlyList<Weapon> weapons)
    {
        if (_game.BattleMap == null)
            return [];
        
        var viableWeapons = new List<WeaponEvaluationData>();
        
        // Check line of sight
        if (!_game.BattleMap.HasLineOfSight(attackerPath.Destination.Coordinates, targetPath.Destination.Coordinates))
            return viableWeapons;

        var distanceToTarget = attackerPath.Destination.Coordinates.DistanceTo(targetPath.Destination.Coordinates);

        foreach (var weapon in weapons)
        {
            // Check if the weapon can fire at this range
            if (distanceToTarget < weapon.MinimumRange || distanceToTarget > weapon.LongRange)
                continue;

            var isInArc =
                attackerPath.Destination.Coordinates.IsInWeaponFiringArc(targetPath.Destination.Coordinates, weapon,
                    attackerPath.Destination.Facing);
            if (!isInArc)
                continue;

            // Calculate hit probability
            var hitProbability = CalculateHitProbability(attacker, attackerPath, targetPath, weapon);

            if (hitProbability <= 0)
                continue;

            viableWeapons.Add(new WeaponEvaluationData
            {
                Weapon = weapon,
                HitProbability = hitProbability,
            });
        }

        return viableWeapons;
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
        if (_game.BattleMap == null || attacker.Pilot == null)
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
            attackerPosition: attackerPath.Destination,
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
