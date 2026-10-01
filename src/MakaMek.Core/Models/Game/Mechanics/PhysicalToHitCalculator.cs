using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Classic BattleTech to-hit calculation for physical attacks: Piloting Skill as the base, a
/// per-type modifier, then the ordinary attacker movement and terrain modifiers. Heat and sensor
/// damage are excluded by the scenario rather than filtered here, so this class has no way to
/// reintroduce them.
/// </summary>
public class PhysicalToHitCalculator(IRulesProvider rules) : IPhysicalToHitCalculator
{
    public int GetToHitNumber(
        IUnit attacker,
        IUnit target,
        PhysicalAttackType attackType,
        PartLocation attackerLimb,
        IBattleMap map) =>
        GetModifierBreakdown(attacker, target, attackType, attackerLimb, map).Total;

    public PhysicalToHitBreakdown GetModifierBreakdown(
        IUnit attacker,
        IUnit target,
        PhysicalAttackType attackType,
        PartLocation attackerLimb,
        IBattleMap map) =>
        Preview(PhysicalAttackScenario.FromUnits(attacker, target, attackType, attackerLimb), map);

    public PhysicalToHitBreakdown Preview(PhysicalAttackScenario scenario, IBattleMap map)
    {
        var losResult = map.GetLineOfSight(
            scenario.AttackerPosition.Coordinates,
            scenario.TargetPosition.Coordinates,
            scenario.AttackerHeight,
            scenario.TargetHeight,
            scenario.AttackerPosition.Surface,
            scenario.TargetPosition.Surface);

        var otherModifiers = new List<RollModifier>(scenario.AttackerModifiers);
        if (scenario.TargetUnit?.IsSkidding == true)
        {
            otherModifiers.Add(new SkiddingTargetModifier
            {
                Value = SkiddingTargetModifier.DefaultValue
            });
        }

        return new PhysicalToHitBreakdown
        {
            PilotingBase = new PilotingRollModifier
            {
                Value = scenario.AttackerPiloting
            },
            AttackTypeBase = PhysicalAttackBaseModifier.Create(scenario.AttackType),
            AttackerMovement = new AttackerMovementModifier
            {
                Value = rules.GetAttackerMovementModifier(scenario.AttackerMovementType),
                MovementType = scenario.AttackerMovementType
            },
            OtherModifiers = otherModifiers,
            TerrainModifiers = GetTerrainModifiers(losResult),
            HasLineOfSight = losResult.HasLineOfSight
        };
    }

    /// <summary>
    /// Same walk of the line of sight the weapon calculator does: intervening hexes only, one
    /// modifier per terrain that actually carries one.
    /// </summary>
    private IReadOnlyList<TerrainRollModifier> GetTerrainModifiers(LineOfSightResult losResult)
    {
        var modifiers = new List<TerrainRollModifier>();
        foreach (var hexInfo in losResult.HexPath)
        {
            if (hexInfo.InterveningFactor <= 0) continue;
            foreach (var terrain in hexInfo.Hex.GetTerrains())
            {
                var modifier = rules.GetTerrainToHitModifier(terrain.Id);
                if (modifier == 0) continue;
                modifiers.Add(new TerrainRollModifier
                {
                    Value = modifier,
                    Location = hexInfo.Hex.Coordinates,
                    TerrainId = terrain.Id
                });
            }
        }
        return modifiers;
    }
}
