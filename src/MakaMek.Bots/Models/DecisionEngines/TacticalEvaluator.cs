using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Models.Map;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units.Components;
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
    /// <param name="turnState">Optional turn state for caching evaluation results</param>
    /// <returns>Position score including the path</returns>
    public async Task<PositionScore> EvaluatePath(
        IUnit unit,
        MovementPath path,
        IReadOnlyList<IUnit> enemyUnits,
        ITurnState? turnState = null)
    {
        var defensiveIndex = CalculateDefensiveIndex(path, enemyUnits);
        var targetScores = await EvaluateTargets(unit, path, enemyUnits, turnState);
        var offensiveIndex = targetScores
            .Sum(t => t.ConfigurationScores.Any()
                ? t.ConfigurationScores.Max(cs => cs.Score)
                : 0);

        return new PositionScore
        {
            Position = path.Destination,
            MovementType = path.MovementType,
            Path = path,
            DefensiveIndex = defensiveIndex.Score,
            OffensiveIndex = offensiveIndex,
            EnemiesInRearArc = defensiveIndex.EnemiesInRearArc
        };
    }
    
    /// <summary>
    /// Evaluates potential targets for a unit
    /// </summary>
    public ValueTask<IReadOnlyList<TargetEvaluationData>> EvaluateTargets(
        IUnit attacker, MovementPath attackerPath, IReadOnlyList<IUnit> potentialTargets, ITurnState? turnState = null)
    {
        if (_game.BattleMap == null || attacker.Position == null)
            return ValueTask.FromResult<IReadOnlyList<TargetEvaluationData>>([]);

        var results = new List<TargetEvaluationData>();

        // Get all friendly weapons
        var weapons = attacker.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .Where(w => w.IsAvailable)
            .ToList();

        foreach (var target in potentialTargets)
        {
            if (target.Position == null)
                continue;
            
            // Construct Key
            var key = new TargetEvaluationKey(
                attacker.Id, attackerPath.Destination.Coordinates, attackerPath.Destination.Facing,
                target.Id, target.Position.Coordinates, target.Position.Facing
            );
            
            if (turnState?.TryGetTargetEvaluation(key, out var cachedData) == true)
            {
                results.Add(cachedData);
                continue;
            }

            var targetPath = target.MovementTaken ?? MovementPath.CreateStandingStillPath(target.Position);
            var viableWeapons = EvaluateWeaponsForTarget(attacker,
                attackerPath,
                targetPath,
                weapons);

            if (viableWeapons.Count <= 0) continue;

            // Determine which arc of the enemy would be hit (bonus for rear/side shots)
            var targetArc = GetFiringArcFromPosition(target.Position, attackerPath.Destination.Coordinates);
            var arcBonus = targetArc.GetArcMultiplier();
            
            // Calculate score for each configuration
            var configScores = new List<WeaponConfigurationEvaluationData>();
            foreach (var (config, weaponsForConfig) in viableWeapons)
            {
                var score = weaponsForConfig.Sum(w =>
                    w.HitProbability * w.Weapon.Damage
                ) * arcBonus;

                configScores.Add(new WeaponConfigurationEvaluationData
                {
                    Configuration = config,
                    Score = score,
                    ViableWeapons = weaponsForConfig
                });
            }
            
            var targetEvaluationData = new TargetEvaluationData
            {
                TargetId = target.Id,
                ConfigurationScores = configScores
            };
            
            results.Add(targetEvaluationData);
            turnState?.AddTargetEvaluation(key, targetEvaluationData);
        }

        return ValueTask.FromResult<IReadOnlyList<TargetEvaluationData>>(results);
    }
    
    /// <summary>
    /// Calculates the defensive threat index for a position.
    /// Considers enemy weapons that can target this position and their hit probabilities.
    /// </summary>
    /// <param name="defenderPath">The position to evaluate and path to get to it</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <returns>Defensive threat index (lower is better)</returns>
    private PathDefensiveScore CalculateDefensiveIndex(
        MovementPath defenderPath,
        IReadOnlyList<IUnit> enemyUnits)
    {
        var enemiesInRearArc = 0;
        if (_game.BattleMap == null)
            return new PathDefensiveScore(0, enemiesInRearArc);

        double defensiveIndex = 0;
        var position = defenderPath.Destination; 
        
        foreach (var enemy in enemyUnits)
        {
            if (enemy.Position == null)
                continue;
            
            var enemyPath = enemy.MovementTaken ?? MovementPath.CreateStandingStillPath(enemy.Position);

            // Determine which arc of the friendly unit would be hit
            var targetArc = GetFiringArcFromPosition(position, enemy.Position.Coordinates);
            if (targetArc == FiringArc.Rear)
                enemiesInRearArc++;
            
            // Check line of sight
            if (!_game.BattleMap.HasLineOfSight(enemy.Position.Coordinates, position.Coordinates))
                continue;

            var range = enemy.Position.Coordinates.DistanceTo(position.Coordinates);

            // Get all enemy weapons
            var weapons = enemy.Parts.Values
                .SelectMany(p => p.GetComponents<Weapon>())
                .Where(w => w.IsAvailable);
            
            
            var arcMultiplier = targetArc.GetArcMultiplier();
            
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
                
                // Calculate threat value
                var threatValue = hitProbability * weapon.Damage * arcMultiplier;
                defensiveIndex += threatValue;
            }
        }

        return new PathDefensiveScore(defensiveIndex, enemiesInRearArc);
    }
    
    /// <summary>
    /// Evaluates all weapons against a target and returns a list of viable weapons with hit probabilities
    /// </summary>
    private Dictionary<WeaponConfiguration, List<WeaponEvaluationData>> EvaluateWeaponsForTarget(
        IUnit attacker,
        MovementPath attackerPath,
        MovementPath targetPath,
        IReadOnlyList<Weapon> weapons)
    {
        if (_game.BattleMap == null)
            return [];

        var configWeapons = new Dictionary<WeaponConfiguration, List<WeaponEvaluationData>>();

        // Check line of sight
        if (!_game.BattleMap.HasLineOfSight(attackerPath.Destination.Coordinates, targetPath.Destination.Coordinates))
            return configWeapons;

        var distanceToTarget = attackerPath.Destination.Coordinates.DistanceTo(targetPath.Destination.Coordinates);

        // Get all weapon configurations (torso rotations)
        var configOptions = attacker.GetWeaponsConfigurationOptions(attackerPath.Destination);
        var torsoConfigs = configOptions
            .Where(o => o.Type == WeaponConfigurationType.TorsoRotation)
            .SelectMany(o => o.AvailableDirections.Select(d =>
                new WeaponConfiguration { Type = WeaponConfigurationType.TorsoRotation, Value = (int)d }))
            .ToList();

        // Add the "no rotation" option (current leg facing)
        var noRotationConfig = new WeaponConfiguration
        {
            Type = WeaponConfigurationType.None,
            Value = (int)attackerPath.Destination.Facing
        };
        var allConfigs = new[] { noRotationConfig }.Concat(torsoConfigs).ToList();

        // Evaluate weapons for each configuration
        foreach (var config in allConfigs)
        {
            var facing = (HexDirection)config.Value;

            var viableWeapons = new List<WeaponEvaluationData>();

            foreach (var weapon in weapons)
            {
                // Check if the weapon can fire at this range
                if (distanceToTarget > weapon.LongRange || weapon.FirstMountPart == null)
                    continue;

                // Check configuration applicability
                if (!weapon.FirstMountPart.IsWeaponConfigurationApplicable(config.Type, attackerPath.Destination))
                    continue;

                // Check arc using resolved facing
                var isInArc =
                    attackerPath.Destination.Coordinates.IsInWeaponFiringArc(targetPath.Destination.Coordinates, weapon,
                        facing);
                if (!isInArc)
                    continue;

                // Calculate hit probability with the configuration's facing
                var hitProbability = CalculateHitProbability(attacker, attackerPath, targetPath, weapon, facing);

                if (hitProbability <= 0)
                    continue;

                viableWeapons.Add(new WeaponEvaluationData
                {
                    Weapon = weapon,
                    HitProbability = hitProbability,
                });
            }

            if (viableWeapons.Count > 0)
            {
                configWeapons[config] = viableWeapons;
            }
        }

        return configWeapons;
    }

    /// <summary>
    /// Calculates hit probability for an enemy attacking the friendly unit at a hypothetical position.
    /// Uses ToHitCalculator with AttackScenario for full accuracy including all modifiers.
    /// </summary>
    private double CalculateHitProbability(
        IUnit attacker,
        MovementPath attackerPath,
        MovementPath targetPath,
        Weapon weapon,
        HexDirection? attackerFacing = null)
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

        // Use provided facing or default to path destination facing
        var facing = attackerFacing ?? attackerPath.Destination.Facing;

        // Create a hypothetical attack scenario
        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: attacker.Pilot.Gunnery,
            attackerPosition: attackerPath.Destination,
            attackerMovementType: attackerMovementType,
            targetPosition: targetPosition,
            targetHexesMoved: targetPath.HexesTraveled,
            attackerModifiers: attackerModifiers,
            attackerFacing: facing
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
