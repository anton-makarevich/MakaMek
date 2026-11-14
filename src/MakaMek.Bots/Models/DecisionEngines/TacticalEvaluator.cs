using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Models.Map;
using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Weapons;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;

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
    public PositionScore EvaluatePath(
        IUnit unit,
        MovementPath path,
        IReadOnlyList<IUnit> enemyUnits,
        ITurnState? turnState = null)
    {
        var defensiveIndex = CalculateDefensiveIndex(path, enemyUnits, unit.Height, unit.Status.HasFlag(UnitStatus.Prone));

        var targetScores = EvaluateTargets(unit, path, enemyUnits, turnState);
        var offensiveIndex = targetScores
            .Sum(t => t.ConfigurationScores.Count > 0
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
    public IReadOnlyList<TargetEvaluationData> EvaluateTargets(
        IUnit attacker, MovementPath attackerPath, IReadOnlyList<IUnit> potentialTargets, ITurnState? turnState = null)
    {
        if (_game.BattleMap == null || attacker.Position == null)
            return [];

        var results = new List<TargetEvaluationData>();

        // Get all friendly weapons
        var weapons = attacker.Parts.Values
            .SelectMany(p => p.GetComponents<Weapon>())
            .Where(w => w.IsAvailable)
            .ToList();

        // Pre-compute weapon configurations once (same for all targets with this attacker+position)
        var allConfigs = BuildWeaponConfigurations(attacker, attackerPath);

        // Pre-compute per-part config type lookup to avoid repeated GetWeaponsConfigurationOptions calls
        var partConfigTypes = attacker.Parts.Values.ToDictionary(
            p => p,
            p => p.GetWeaponsConfigurationOptions(attackerPath.Destination)
                  .Select(o => o.Type)
                  .ToHashSet());

        foreach (var target in potentialTargets)
        {
            if (target.Position == null)
                continue;
            
            // Construct Key
            var key = new TargetEvaluationKey(
                attacker.Id, attackerPath.Destination.Coordinates, attackerPath.Destination.Facing,
                attackerPath.MovementType,
                target.Id, target.Position.Coordinates, target.Position.Facing,
                target.Position.AirborneLevel
            );
            
            if (turnState?.TryGetTargetEvaluation(key, out var cachedData) == true)
            {
                results.Add(cachedData);
                continue;
            }

            var targetPath = target.MovementTaken ?? MovementPath.CreateSingleSegmentPath(target.Position);
            var viableWeapons = EvaluateWeaponsForTarget(attacker,
                attackerPath,
                target.Position,
                targetPath,
                weapons,
                allConfigs,
                partConfigTypes,
                target.Height,
                target.Status.HasFlag(UnitStatus.Prone));

            if (viableWeapons.Count <= 0) continue;

            var targetEvaluationData = new TargetEvaluationData
            {
                TargetId = target.Id,
                ConfigurationScores = GetConfigurationScores(
                    viableWeapons,
                    target.Position,
                    attackerPath.Destination)
            };
            
            results.Add(targetEvaluationData);
            turnState?.AddTargetEvaluation(key, targetEvaluationData);
        }

        return results;
    }

    /// <summary>
    /// Scores every viable weapon configuration against a target, including the arc bonus.
    /// </summary>
    private static List<WeaponConfigurationEvaluationData> GetConfigurationScores(
        IReadOnlyList<ConfigurationWeapons> viableWeapons,
        HexPosition targetPosition,
        HexPosition attackerDestination)
    {
        // Determine which arc of the enemy would be hit (bonus for rear/side shots)
        var arcBonus = GetFiringArcFromPosition(targetPosition, attackerDestination.Coordinates).GetArcMultiplier();

        return viableWeapons
            .Select(configWeapons => new WeaponConfigurationEvaluationData
            {
                Configuration = configWeapons.Configuration,
                Score = GetConfigurationScore(configWeapons.ViableWeapons) * arcBonus,
                ViableWeapons = configWeapons.ViableWeapons
            })
            .ToList();
    }

    /// <summary>
    /// Sums the expected damage of a configuration's viable weapons.
    /// </summary>
    private static double GetConfigurationScore(IReadOnlyList<WeaponEvaluationData> viableWeapons)
        => viableWeapons.Sum(w => w.HitProbability * w.Weapon.Damage);
    
    /// <summary>
    /// Calculates the defensive threat index for a position.
    /// Considers enemy weapons that can target this position and their hit probabilities.
    /// </summary>
    /// <param name="defenderPath">The position to evaluate and path to get to it</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <param name="defenderHeight">Height of the unit at the position</param>
    /// <returns>Defensive threat index (lower is better)</returns>
    private PathDefensiveScore CalculateDefensiveIndex(
        MovementPath defenderPath,
        IReadOnlyList<IUnit> enemyUnits,
        int defenderHeight,
        bool defenderIsProne = false)
    {
        var battleMap = _game.BattleMap;
        if (battleMap == null)
            return new PathDefensiveScore(0, 0);

        double defensiveIndex = 0;
        var enemiesInRearArc = 0;
        var position = defenderPath.Destination;

        foreach (var enemy in enemyUnits)
        {
            var enemyPosition = enemy.Position;
            if (enemyPosition == null)
                continue;

            // Determine which arc of the friendly unit would be hit
            var targetArc = GetFiringArcFromPosition(position, enemyPosition.Coordinates);
            if (targetArc == FiringArc.Rear)
                enemiesInRearArc++;

            var enemyPath = enemy.MovementTaken ?? MovementPath.CreateSingleSegmentPath(enemyPosition);
            foreach (var threatValue in GetWeaponThreats(
                         battleMap, enemy, enemyPath, defenderPath, targetArc, defenderHeight, defenderIsProne))
            {
                defensiveIndex += threatValue;
            }
        }

        return new PathDefensiveScore(defensiveIndex, enemiesInRearArc);
    }

    /// <summary>
    /// Yields the threat value of every enemy weapon that can effectively fire at the defended position.
    /// </summary>
    private IEnumerable<double> GetWeaponThreats(
        IBattleMap battleMap,
        IUnit enemy,
        MovementPath enemyPath,
        MovementPath defenderPath,
        FiringArc targetArc,
        int defenderHeight,
        bool defenderIsProne)
    {
        var enemyPosition = enemy.Position;
        if (enemyPosition == null
            || !HasLineOfSight(battleMap, enemyPosition, defenderPath.Destination, enemy.Height, defenderHeight))
            yield break;

        var range = enemyPosition.Coordinates.DistanceTo(defenderPath.Destination.Coordinates);
        var arcMultiplier = targetArc.GetArcMultiplier();

        foreach (var weapon in enemy.GetAvailableWeapons())
        {
            // Check if the weapon can fire at this range (minimum range penalty is handled by ToHitCalculator)
            if (!CanReachRange(weapon, range))
                continue;

            // Check firing arc
            if (!enemyPosition.Coordinates.IsInWeaponFiringArc(defenderPath.Destination.Coordinates, weapon))
                continue;

            // Calculate hit probability using ToHitCalculator with full accuracy
            // For defensive calculation, we're the target. A pending movement attack enemy
            // (such as a DFA attacker) is at its current approach position; MovementTaken is
            // still used for its movement type.
            var hitProbability = CalculateHitProbability(enemy,
                enemyPath,
                enemyPosition,
                defenderPath,
                defenderPath.Destination,
                weapon,
                null,
                defenderHeight,
                defenderIsProne);

            // Calculate threat value
            yield return hitProbability * weapon.Damage * arcMultiplier;
        }
    }
    
    /// <summary>
    /// Builds the list of weapon configurations (no-rotation plus torso rotations) for an attacker at a given position.
    /// Computed once per attacker+position and reused across all targets.
    /// </summary>
    private IReadOnlyList<WeaponConfiguration> BuildWeaponConfigurations(IUnit attacker, MovementPath attackerPath)
    {
        var configOptions = attacker.GetWeaponsConfigurationOptions(attackerPath.Destination);
        var torsoConfigs = configOptions
            .Where(o => o.Type == WeaponConfigurationType.TorsoRotation)
            .SelectMany(o => o.AvailableDirections.Select(d =>
                new WeaponConfiguration { Type = WeaponConfigurationType.TorsoRotation, Value = (int)d }))
            .ToList();

        // Prepend the "no rotation" option (current leg facing)
        torsoConfigs.Insert(0, new WeaponConfiguration
        {
            Type = WeaponConfigurationType.None,
            Value = (int)attackerPath.Destination.Facing
        });

        return torsoConfigs;
    }

    /// <summary>
    /// Evaluates all weapons against a target and returns the viable weapons per weapon configuration
    /// </summary>
    private List<ConfigurationWeapons> EvaluateWeaponsForTarget(
        IUnit attacker,
        MovementPath attackerPath,
        HexPosition targetPosition,
        MovementPath targetPath,
        IReadOnlyList<Weapon> weapons,
        IReadOnlyList<WeaponConfiguration> allConfigs,
        Dictionary<UnitPart, HashSet<WeaponConfigurationType>> partConfigTypes,
        int targetHeight,
        bool isTargetProne = false)
    {
        var battleMap = _game.BattleMap;
        if (battleMap == null)
            return [];

        // Check line of sight. Use the target's current position: for a pending DFA attacker that
        // is the airborne approach position, while MovementTaken still describes the full jump.
        if (!HasLineOfSight(battleMap, attackerPath.Destination, targetPosition, attacker.Height, targetHeight))
            return [];

        var distanceToTarget = attackerPath.Destination.Coordinates.DistanceTo(targetPosition.Coordinates);

        var configurationWeapons = new List<ConfigurationWeapons>();

        // Evaluate weapons for each configuration
        foreach (var config in allConfigs)
        {
            var viableWeapons = EvaluateConfiguration(attacker,
                attackerPath,
                targetPosition,
                targetPath,
                weapons,
                config,
                partConfigTypes,
                distanceToTarget,
                targetHeight,
                isTargetProne);

            if (viableWeapons.Count > 0)
                configurationWeapons.Add(new ConfigurationWeapons(config, viableWeapons));
        }

        return configurationWeapons;
    }

    /// <summary>
    /// Evaluates all weapons for a single weapon configuration and returns the viable ones.
    /// </summary>
    private List<WeaponEvaluationData> EvaluateConfiguration(
        IUnit attacker,
        MovementPath attackerPath,
        HexPosition targetPosition,
        MovementPath targetPath,
        IReadOnlyList<Weapon> weapons,
        WeaponConfiguration config,
        IReadOnlyDictionary<UnitPart, HashSet<WeaponConfigurationType>> partConfigTypes,
        int distanceToTarget,
        int targetHeight,
        bool isTargetProne)
    {
        var facing = (HexDirection)config.Value;
        var viableWeapons = new List<WeaponEvaluationData>();

        foreach (var weapon in weapons)
        {
            // Calculate hit probability with the configuration's facing
            var evaluation = EvaluateWeapon(attacker,
                attackerPath,
                targetPosition,
                targetPath,
                weapon,
                config,
                facing,
                partConfigTypes,
                distanceToTarget,
                targetHeight,
                isTargetProne);

            if (evaluation.HasValue)
                viableWeapons.Add(evaluation.Value);
        }

        return viableWeapons;
    }

    /// <summary>
    /// Evaluates a single weapon for a configuration, returning null when it cannot contribute.
    /// </summary>
    private WeaponEvaluationData? EvaluateWeapon(
        IUnit attacker,
        MovementPath attackerPath,
        HexPosition targetPosition,
        MovementPath targetPath,
        Weapon weapon,
        WeaponConfiguration config,
        HexDirection facing,
        IReadOnlyDictionary<UnitPart, HashSet<WeaponConfigurationType>> partConfigTypes,
        int distanceToTarget,
        int targetHeight,
        bool isTargetProne)
    {
        // Check if the weapon can fire at this range
        if (weapon.FirstMountPart == null || !CanReachRange(weapon, distanceToTarget))
            return null;

        // Check configuration applicability (using pre-computed lookup instead of per-call traversal)
        if (!IsConfigurationApplicable(weapon.FirstMountPart, config, partConfigTypes))
            return null;

        // Check arc using resolved facing
        if (!attackerPath.Destination.Coordinates.IsInWeaponFiringArc(targetPosition.Coordinates, weapon, facing))
            return null;

        var hitProbability = CalculateHitProbability(attacker,
            attackerPath,
            attackerPath.Destination,
            targetPath,
            targetPosition,
            weapon,
            facing,
            targetHeight,
            isTargetProne);

        if (hitProbability <= 0)
            return null;

        return new WeaponEvaluationData
        {
            Weapon = weapon,
            HitProbability = hitProbability,
        };
    }

    /// <summary>
    /// Checks whether a weapon can reach the given range.
    /// </summary>
    private static bool CanReachRange(Weapon weapon, int range)
        => weapon.Range != null && range <= weapon.Range.LongRange;

    /// <summary>
    /// Checks whether a weapon's mounting part supports the given configuration.
    /// </summary>
    private static bool IsConfigurationApplicable(
        UnitPart mountPart,
        WeaponConfiguration config,
        IReadOnlyDictionary<UnitPart, HashSet<WeaponConfigurationType>> partConfigTypes)
    {
        if (config.Type == WeaponConfigurationType.None)
            return true;

        return partConfigTypes.TryGetValue(mountPart, out var allowedTypes)
               && allowedTypes.Contains(config.Type);
    }

    /// <summary>
    /// Checks line of sight between two positions.
    /// </summary>
    private static bool HasLineOfSight(
        IBattleMap battleMap,
        HexPosition from,
        HexPosition to,
        int fromHeight,
        int toHeight)
        => battleMap.GetLineOfSight(from, to, fromHeight, toHeight).HasLineOfSight;

    /// <summary>
    /// Calculates hit probability for an enemy attacking the friendly unit at a hypothetical position.
    /// Uses ToHitCalculator with AttackScenario for full accuracy including all modifiers.
    /// </summary>
    private double CalculateHitProbability(
        IUnit attacker,
        MovementPath attackerPath,
        HexPosition attackerPosition,
        MovementPath targetPath,
        HexPosition targetPosition,
        Weapon weapon,
        HexDirection? attackerFacing = null,
        int targetHeight = 0,
        bool isTargetProne = false)
    {
        if (_game.BattleMap == null || attacker.Pilot == null)
            return 0;

        // Get weapon location for attack modifiers
        var weaponLocation = weapon.FirstMountPartLocation;
        if (weaponLocation == null)
            return 0;

        // Get current attack modifiers from the attacker (heat, prone, sensors, arm actuators, etc.)
        var attackerModifiers = attacker.GetAttackModifiers(weaponLocation.Value);

        // Determine attacker's movement type (use actual if available, otherwise assume it will walk for now)
        var attackerMovementType = attackerPath.MovementType;

        // Use provided facing or default to the position's facing
        var facing = attackerFacing ?? attackerPosition.Facing;

        // Compute water depth at the attacker's evaluated position
        var attackerDestinationHex = _game.BattleMap.GetHex(attackerPosition.Coordinates);
        var attackerWaterDepth = attackerDestinationHex?.GetWaterDepth();

        // Compute water depth at the target's evaluated position
        var targetDestinationHex = _game.BattleMap.GetHex(targetPosition.Coordinates);
        var targetWaterDepth = targetDestinationHex?.GetWaterDepth();

        // Create a hypothetical attack scenario with position-based submersion and airborne LOS
        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: attacker.Pilot.Gunnery,
            attackerPosition: attackerPosition,
            attackerMovementType: attackerMovementType,
            targetPosition: targetPosition,
            targetHexesMoved: targetPath.HexesTraveled,
            attackerModifiers: attackerModifiers,
            attackerFacing: facing,
            attackerHeight: attacker.Height,
            targetHeight: targetHeight,
            attackerWaterDepth: attackerWaterDepth,
            targetWaterDepth: targetWaterDepth,
            isTargetProne: isTargetProne
            );

        // Use ToHitCalculator with full accuracy (includes terrain, heat, damage, etc.)
        var toHitNumber = _game.ToHitCalculator.GetToHitNumber(scenario, weapon, _game.BattleMap);

        return DiceUtils.Calculate2d6Probability(toHitNumber);
    }
    
    /// <summary>
    /// Gets the firing arc from which a position would be hit
    /// </summary>
    private static FiringArc GetFiringArcFromPosition(HexPosition unitPosition, HexCoordinates attackerPosition)
        => unitPosition.Coordinates.GetFiringArc(attackerPosition, unitPosition.Facing);

    /// <summary>
    /// Viable weapons for a single weapon configuration.
    /// </summary>
    private sealed record ConfigurationWeapons(
        WeaponConfiguration Configuration,
        IReadOnlyList<WeaponEvaluationData> ViableWeapons);
}
