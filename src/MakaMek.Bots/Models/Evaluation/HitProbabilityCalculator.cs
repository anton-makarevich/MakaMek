using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Utils;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Bots.Models.Evaluation;

/// <summary>
/// Hit probability calculator that builds a hypothetical <see cref="AttackScenario"/> and asks
/// the game's to-hit calculator, including terrain, heat and damage modifiers.
/// </summary>
public sealed class HitProbabilityCalculator(IClientGame game) : IHitProbabilityCalculator
{
    public double Calculate(
        UnitSnapshot attacker,
        MovementPath attackerPath,
        HexPosition attackerPosition,
        MovementPath targetPath,
        HexPosition targetPosition,
        WeaponSnapshot weapon,
        HexDirection? attackerFacing,
        int targetHeight,
        bool isTargetProne)
    {
        var battleMap = game.BattleMap;
        if (battleMap == null || attacker.Gunnery == null)
            return 0;

        if (weapon.MountLocation == null)
            return 0;

        // Use the supplied facing or default to the position's facing
        var facing = attackerFacing ?? attackerPosition.Facing;

        // Compute water depth at the attacker's evaluated position
        var attackerDestinationHex = battleMap.GetHex(attackerPosition.Coordinates);
        var attackerWaterDepth = attackerDestinationHex?.GetWaterDepth();

        // Compute water depth at the target's evaluated position
        var targetDestinationHex = battleMap.GetHex(targetPosition.Coordinates);
        var targetWaterDepth = targetDestinationHex?.GetWaterDepth();

        // Create a hypothetical attack scenario with position-based submersion and airborne LOS
        var scenario = AttackScenario.FromHypothetical(
            attackerGunnery: attacker.Gunnery.Value,
            attackerPosition: attackerPosition,
            attackerMovementType: attackerPath.MovementType,
            targetPosition: targetPosition,
            targetHexesMoved: targetPath.HexesTraveled,
            attackerModifiers: weapon.AttackModifiers,
            attackerFacing: facing,
            attackerHeight: attacker.Height,
            targetHeight: targetHeight,
            attackerWaterDepth: attackerWaterDepth,
            targetWaterDepth: targetWaterDepth,
            isTargetProne: isTargetProne
        );

        var toHitNumber = game.ToHitCalculator.GetToHitNumber(scenario, weapon.Weapon, battleMap);
        return DiceUtils.Calculate2d6Probability(toHitNumber);
    }
}
