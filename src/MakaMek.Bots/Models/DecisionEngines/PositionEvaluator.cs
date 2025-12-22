using Sanet.MakaMek.Bots.Models.Map;
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

            // TODO: evaluate how to use IHitCalculator for that
            foreach (var weapon in weapons)
            {
                // Check if weapon can fire at this range
                if (range < weapon.MinimumRange || range > weapon.LongRange)
                    continue;

                // Check if target position is in weapon's firing arc
                var weaponLocation = weapon.FirstMountPartLocation;
                if (weaponLocation == null)
                    continue;

                // Determine weapon facing (torso for torso/arm weapons, legs for leg weapons)
                var weaponFacing = enemy is Core.Models.Units.Mechs.Mech mech && 
                                   (weaponLocation == PartLocation.LeftArm || weaponLocation == PartLocation.RightArm ||
                                    weaponLocation == PartLocation.LeftTorso || weaponLocation == PartLocation.RightTorso ||
                                    weaponLocation == PartLocation.CenterTorso)
                    ? mech.TorsoDirection ?? enemy.Position.Facing
                    : enemy.Position.Facing;

                // Check firing arc
                var isInArc = IsWeaponInArc(enemy.Position.Coordinates, weaponFacing, weaponLocation.Value, position.Coordinates);
                if (!isInArc)
                    continue;

                // Create a temporary unit state to simulate the movement for to-hit calculation
                // We need to calculate what the to-hit would be if the friendly unit were at this position
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
                // Check if weapon can fire at this range
                if (range < weapon.MinimumRange || range > weapon.LongRange)
                    continue;

                // Check if enemy is in weapon's firing arc
                var weaponLocation = weapon.FirstMountPartLocation;
                if (weaponLocation == null)
                    continue;

                // Determine weapon facing from the position (assume only forward facing for now)
                var weaponFacing =  position.Facing;

                var isInArc = IsWeaponInArc(position.Coordinates, weaponFacing, weaponLocation.Value, enemy.Position.Coordinates);
                if (!isInArc)
                    continue;

                // Calculate hit probability with movement type affecting attacker movement modifier
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
        List<PathSegment> path,
        MovementType movementType,
        IUnit friendlyUnit,
        IReadOnlyList<IUnit> enemyUnits)
    {
        // Extract position and hexesTraveled from the path
        var position = path.Count > 0 ? path[^1].To : friendlyUnit.Position!;
        var hexesTraveled = path.Count(segment => segment.From.Coordinates != segment.To.Coordinates);

        var defensiveIndex = CalculateDefensiveIndex(position, hexesTraveled, enemyUnits);
        var offensiveIndex = CalculateOffensiveIndex(position, movementType, friendlyUnit, enemyUnits);

        return new PositionScore
        {
            Position = position,
            MovementType = movementType,
            HexesTraveled = hexesTraveled,
            Path = path,
            DefensiveIndex = defensiveIndex,
            OffensiveIndex = offensiveIndex
        };
    }

    /// <summary>
    /// Calculates hit probability for an enemy attacking the friendly unit at a position
    /// </summary>
    private double CalculateHitProbability(IUnit attacker, Weapon weapon, HexPosition targetPosition, int targetHexesMoved)
    {
        if (_game.BattleMap == null || attacker.Position == null)
            return 0;

        // We can't directly use ToHitCalculator because the target hasn't actually moved yet
        // TODO: ToHitCalculator should be updated to handle this case
        // So we need to estimate the to-hit number based on the position
        // For simplicity, we'll use the base gunnery skill + range + movement modifiers
        
        var distance = attacker.Position.Coordinates.DistanceTo(targetPosition.Coordinates);
        var range = weapon.GetRangeBracket(distance);
        
        if (range == WeaponRange.OutOfRange)
            return 0;

        // Estimate to-hit number
        var toHitNumber = attacker.Pilot?.Gunnery ?? 4; // Default gunnery
        toHitNumber += _game.RulesProvider.GetRangeModifier(range, weapon.LongRange, distance);
        toHitNumber += _game.RulesProvider.GetTargetMovementModifier(targetHexesMoved);
        
        // Add attacker's movement modifier (enemy's last movement)
        if (attacker.MovementTypeUsed.HasValue)
        {
            toHitNumber += _game.RulesProvider.GetAttackerMovementModifier(attacker.MovementTypeUsed.Value);
        }

        return DiceUtils.Calculate2d6Probability(toHitNumber);
    }

    /// <summary>
    /// Calculates hit probability for the friendly unit attacking from a position
    /// </summary>
    private double CalculateHitProbabilityAsAttacker(IUnit attacker, IUnit target, Weapon weapon, HexPosition attackerPosition, MovementType movementType)
    {
        if (_game.BattleMap == null || target.Position == null)
            return 0;

        var distance = attackerPosition.Coordinates.DistanceTo(target.Position.Coordinates);
        var range = weapon.GetRangeBracket(distance);
        
        if (range == WeaponRange.OutOfRange)
            return 0;

        // Estimate to-hit number
        var toHitNumber = attacker.Pilot?.Gunnery ?? 4; // Default gunnery
        toHitNumber += _game.RulesProvider.GetRangeModifier(range, weapon.LongRange, distance);
        toHitNumber += _game.RulesProvider.GetAttackerMovementModifier(movementType);
        
        // Add target's movement modifier (enemy's actual movement)
        toHitNumber += _game.RulesProvider.GetTargetMovementModifier(target.DistanceCovered);

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
