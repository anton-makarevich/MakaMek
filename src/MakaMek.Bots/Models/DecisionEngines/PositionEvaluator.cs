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
    /// Takes into account the movement type used to reach this position for target movement modifiers.
    /// </summary>
    /// <param name="position">The position to evaluate</param>
    /// <param name="hexesTraveled">The number of hexes traveled to reach this position</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <returns>Defensive threat index (lower is better)</returns>
    private double CalculateDefensiveIndex(
        HexPosition position,
        int hexesTraveled,
        IReadOnlyList<IUnit> enemyUnits)
    {
        if (_game.BattleMap == null)
            return 0;

        double defensiveIndex = 0;

        foreach (var enemy in enemyUnits)
        {
            if (enemy.Position == null)
                continue;

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

                // Check if target position is in weapon's firing arc
                var weaponLocation = weapon.FirstMountPartLocation;
                if (weaponLocation == null)
                    continue;

                // Determine weapon facing (torso for torso/arm weapons, legs for leg weapons)
                var weaponFacing = weapon.Facing;
                if (weaponFacing == null)
                    continue;

                // Check firing arc
                var isInArc = IsWeaponInArc(enemy.Position.Coordinates, weaponFacing.Value, weaponLocation.Value, position.Coordinates);
                if (!isInArc)
                    continue;

                // Calculate hit probability using ToHitCalculator with full accuracy
                // For defensive calculation, we're the target, so we use hexesTraveled for target movement modifier
                var hitProbability = CalculateHitProbability(enemy, weapon, position, hexesTraveled);

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
    /// <param name="position">The position to evaluate</param>
    /// <param name="movementType">The movement type used to reach this position</param>
    /// <param name="friendlyUnit">The friendly unit being evaluated</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <returns>Offensive potential index (higher is better)</returns>
    private double CalculateOffensiveIndex(
        HexPosition position,
        MovementType movementType,
        IUnit friendlyUnit,
        IReadOnlyList<IUnit> enemyUnits)
    {
        if (_game.BattleMap == null)
            return 0;

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

            // Check line of sight
            if (!_game.BattleMap.HasLineOfSight(position.Coordinates, enemy.Position.Coordinates))
                continue;

            var range = position.Coordinates.DistanceTo(enemy.Position.Coordinates);

            foreach (var weapon in weapons)
            {
                // Check if the weapon can fire at this range
                if (range < weapon.MinimumRange || range > weapon.LongRange)
                    continue;

                // Check if enemy is in weapon's firing arc
                var weaponLocation = weapon.FirstMountPartLocation;
                if (weaponLocation == null)
                    continue;

                // Determine weapon facing from the position (assume only forward facing for now)
                var weaponFacing =  weapon.Facing;
                if (weaponFacing == null)
                    continue;

                var isInArc = IsWeaponInArc(position.Coordinates, weaponFacing.Value, weaponLocation.Value, enemy.Position.Coordinates);
                if (!isInArc)
                    continue;

                // Calculate hit probability with a movement type affecting the attacker movement modifier
                var hitProbability = CalculateHitProbabilityAsAttacker(friendlyUnit, enemy, weapon, position, movementType);

                // Determine which arc of the enemy would be hit (bonus for rear/side shots)
                var targetArc = GetFiringArcFromPosition(enemy.Position, position.Coordinates);
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
    /// <param name="path">The movement path to evaluate</param>
    /// <param name="movementType">The movement type used</param>
    /// <param name="friendlyUnit">The friendly unit being evaluated</param>
    /// <param name="enemyUnits">All enemy units</param>
    /// <returns>Position score including the path</returns>
    public PositionScore EvaluatePath(
        MovementPath path,
        MovementType movementType,
        IUnit friendlyUnit,
        IReadOnlyList<IUnit> enemyUnits)
    {
        // Extract position and hexesTraveled from the path
        var position = path.Destination?? friendlyUnit.Position;
        var hexesTraveled = path.DistanceCovered;

        if (position == null)
        {
            throw new InvalidOperationException("Path destination is null");
        }

        var defensiveIndex = CalculateDefensiveIndex(position, hexesTraveled, enemyUnits);
        var offensiveIndex = CalculateOffensiveIndex(position, movementType, friendlyUnit, enemyUnits);

        return new PositionScore
        {
            Position = position,
            MovementType = movementType,
            Path = path,
            DefensiveIndex = defensiveIndex,
            OffensiveIndex = offensiveIndex
        };
    }

    /// <summary>
    /// Calculates hit probability for an enemy attacking the friendly unit at a hypothetical position.
    /// Uses ToHitCalculator with AttackScenario for full accuracy including all modifiers.
    /// </summary>
    private double CalculateHitProbability(IUnit attacker, Weapon weapon, HexPosition targetPosition, int targetHexesMoved)
    {
        if (_game.BattleMap == null || attacker.Position == null || attacker.Pilot == null)
            return 0;

        var distance = attacker.Position.Coordinates.DistanceTo(targetPosition.Coordinates);
        var range = weapon.GetRangeBracket(distance);

        if (range == WeaponRange.OutOfRange)
            return 0;

        // Get weapon location for attack modifiers
        var weaponLocation = weapon.FirstMountPartLocation ?? PartLocation.CenterTorso;

        // Get current attack modifiers from the attacker (heat, prone, sensors, arm actuators, etc.)
        var attackerModifiers = attacker.GetAttackModifiers(weaponLocation);

        // Determine attacker's movement type (use actual if available, otherwise assume it will walk for now)
        var attackerMovementType = attacker.MovementTypeUsed ?? MovementType.Walk;

        // Create a hypothetical attack scenario
        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: attacker.Pilot.Gunnery,
            attackerPosition: attacker.Position,
            attackerMovementType: attackerMovementType,
            targetPosition: targetPosition,
            targetHexesMoved: targetHexesMoved,
            attackerModifiers: attackerModifiers,
            attackerFacing: attacker.Position.Facing);

        // Use ToHitCalculator with full accuracy (includes terrain, heat, damage, etc.)
        var toHitNumber = _game.ToHitCalculator.GetToHitNumber(scenario, weapon, _game.BattleMap);

        return DiceUtils.Calculate2d6Probability(toHitNumber);
    }

    /// <summary>
    /// Calculates hit probability for the friendly unit attacking from a hypothetical position.
    /// Uses ToHitCalculator with AttackScenario for full accuracy including all modifiers.
    /// </summary>
    private double CalculateHitProbabilityAsAttacker(IUnit attacker, IUnit target, Weapon weapon, HexPosition attackerPosition, MovementType movementType)
    {
        if (_game.BattleMap == null || target.Position == null || attacker.Pilot == null)
            return 0;

        var distance = attackerPosition.Coordinates.DistanceTo(target.Position.Coordinates);
        var range = weapon.GetRangeBracket(distance);

        if (range == WeaponRange.OutOfRange)
            return 0;

        // Get weapon location for attack modifiers
        var weaponLocation = weapon.FirstMountPartLocation ?? PartLocation.CenterTorso;

        // Get current attack modifiers from the attacker (heat, prone, sensors, arm actuators, etc.)
        // Note: Unit state (heat, damage) doesn't change during Movement and Attack phases,
        // so we can use the current state for hypothetical evaluation
        var attackerModifiers = attacker.GetAttackModifiers(weaponLocation);

        // Create a hypothetical attack scenario
        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: attacker.Pilot.Gunnery,
            attackerPosition: attackerPosition,
            attackerMovementType: movementType,
            targetPosition: target.Position,
            targetHexesMoved: target.DistanceCovered,
            attackerModifiers: attackerModifiers,
            attackerFacing: attackerPosition.Facing);

        // Use ToHitCalculator with full accuracy (includes terrain, heat, damage, etc.)
        var toHitNumber = _game.ToHitCalculator.GetToHitNumber(scenario, weapon, _game.BattleMap);

        return DiceUtils.Calculate2d6Probability(toHitNumber);
    }

    /// <summary>
    /// Determines if a weapon can fire at a target based on its firing arc
    /// </summary>
    private bool IsWeaponInArc(HexCoordinates weaponPosition, HexDirection weaponFacing, PartLocation weaponLocation, HexCoordinates targetPosition)
    {
        // Arms can fire in front and side arcs
        if (weaponLocation == PartLocation.LeftArm)
        {
            return weaponPosition.IsInFiringArc(targetPosition, weaponFacing, FiringArc.Front) ||
                   weaponPosition.IsInFiringArc(targetPosition, weaponFacing, FiringArc.Left);
        }
        
        if (weaponLocation == PartLocation.RightArm)
        {
            return weaponPosition.IsInFiringArc(targetPosition, weaponFacing, FiringArc.Front) ||
                   weaponPosition.IsInFiringArc(targetPosition, weaponFacing, FiringArc.Right);
        }

        // All other weapons (torso, legs, head) can only fire forward
        return weaponPosition.IsInFiringArc(targetPosition, weaponFacing, FiringArc.Front);
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
