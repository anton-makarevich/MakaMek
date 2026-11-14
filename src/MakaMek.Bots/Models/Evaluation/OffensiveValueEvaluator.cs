using Sanet.MakaMek.Bots.Data;
using Sanet.MakaMek.Bots.Models.Map;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Offensive evaluation of weapon configurations against a target, driven by projected snapshots.
/// </summary>
public sealed class OffensiveValueEvaluator(
    ILineOfSightEvaluator lineOfSight,
    IHitProbabilityCalculator hitProbability) : IOffensiveValueEvaluator
{
    public TargetEvaluationData? Evaluate(UnitSnapshot attacker, MovementPath attackerPath, UnitSnapshot target)
    {
        var targetPosition = target.Position;
        if (targetPosition == null)
            return null;

        // Check line of sight. Use the target's current position: for a pending DFA attacker that
        // is the airborne approach position, while MovementTaken still describes the full jump.
        if (!lineOfSight.HasLineOfSight(
                attackerPath.Destination,
                targetPosition,
                attacker.Height,
                target.Height))
            return null;

        var targetPath = target.MovementPath ?? MovementPath.CreateSingleSegmentPath(targetPosition);
        var configurationScores = EvaluateWeaponsForTarget(
            attacker,
            attackerPath,
            targetPosition,
            targetPath,
            target.Height,
            target.IsProne);

        if (configurationScores.Count == 0)
            return null;

        return new TargetEvaluationData
        {
            TargetId = target.Id,
            ConfigurationScores = configurationScores
        };
    }

    /// <summary>
    /// Evaluates all weapons against a target and returns the scores per weapon configuration.
    /// </summary>
    private List<WeaponConfigurationEvaluationData> EvaluateWeaponsForTarget(
        UnitSnapshot attacker,
        MovementPath attackerPath,
        HexPosition targetPosition,
        MovementPath targetPath,
        int targetHeight,
        bool isTargetProne)
    {
        var distanceToTarget = attackerPath.Destination.Coordinates.DistanceTo(targetPosition.Coordinates);

        var viableWeapons = new List<ConfigurationWeapons>();
        foreach (var config in attacker.Configurations)
        {
            var weapons = EvaluateConfiguration(
                attacker,
                attackerPath,
                targetPosition,
                targetPath,
                config,
                distanceToTarget,
                targetHeight,
                isTargetProne);

            if (weapons.Count > 0)
                viableWeapons.Add(new ConfigurationWeapons(config, weapons));
        }

        if (viableWeapons.Count == 0)
            return [];

        // Determine which arc of the enemy would be hit (bonus for rear/side shots)
        var arcBonus = targetPosition.Coordinates
            .GetFiringArc(attackerPath.Destination.Coordinates, targetPosition.Facing)
            .GetArcMultiplier();

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
    /// Evaluates all weapons for a single weapon configuration and returns the viable ones.
    /// </summary>
    private List<WeaponEvaluationData> EvaluateConfiguration(
        UnitSnapshot attacker,
        MovementPath attackerPath,
        HexPosition targetPosition,
        MovementPath targetPath,
        WeaponConfiguration config,
        int distanceToTarget,
        int targetHeight,
        bool isTargetProne)
    {
        var facing = (HexDirection)config.Value;
        var viableWeapons = new List<WeaponEvaluationData>();

        foreach (var weapon in attacker.Weapons)
        {
            var evaluation = EvaluateWeapon(
                attacker,
                attackerPath,
                targetPosition,
                targetPath,
                weapon,
                config,
                facing,
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
        UnitSnapshot attacker,
        MovementPath attackerPath,
        HexPosition targetPosition,
        MovementPath targetPath,
        WeaponSnapshot weapon,
        WeaponConfiguration config,
        HexDirection facing,
        int distanceToTarget,
        int targetHeight,
        bool isTargetProne)
    {
        // Check if the weapon can fire at this range
        if (weapon.MountLocation == null || !weapon.CanReach(distanceToTarget))
            return null;

        // Check configuration applicability (using pre-computed lookup instead of per-call traversal)
        if (!IsConfigurationApplicable(weapon, config, attacker.ConfigurationTypesByPart))
            return null;

        // Check arc using resolved facing
        if (!weapon.IsInFiringArc(
                attackerPath.Destination.Coordinates,
                targetPosition.Coordinates,
                facing))
            return null;

        var probability = hitProbability.Calculate(
            attacker,
            attackerPath,
            attackerPath.Destination,
            targetPath,
            targetPosition,
            weapon,
            facing,
            targetHeight,
            isTargetProne);

        if (probability <= 0)
            return null;

        return new WeaponEvaluationData
        {
            Weapon = weapon.Weapon,
            HitProbability = probability,
        };
    }

    /// <summary>
    /// Sums the expected damage of a configuration's viable weapons.
    /// </summary>
    private static double GetConfigurationScore(IReadOnlyList<WeaponEvaluationData> viableWeapons)
        => viableWeapons.Sum(w => w.HitProbability * w.Weapon.Damage);

    /// <summary>
    /// Checks whether a weapon's mounting part supports the given configuration.
    /// </summary>
    private static bool IsConfigurationApplicable(
        WeaponSnapshot weapon,
        WeaponConfiguration config,
        IReadOnlyDictionary<PartLocation, HashSet<WeaponConfigurationType>> configurationTypesByPart)
    {
        if (config.Type == WeaponConfigurationType.None)
            return true;

        return weapon.MountLocation != null
               && configurationTypesByPart.TryGetValue(weapon.MountLocation.Value, out var allowedTypes)
               && allowedTypes.Contains(config.Type);
    }

    /// <summary>
    /// Viable weapons for a single weapon configuration.
    /// </summary>
    private sealed record ConfigurationWeapons(
        WeaponConfiguration Configuration,
        IReadOnlyList<WeaponEvaluationData> ViableWeapons);
}
