using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Classic BattleTech implementation of the physical attack to-hit calculator.
/// Base to-hit is the attacker's Piloting Skill plus a per-type base modifier
/// (Punch +0, Kick -2); standard attacker movement, target movement and terrain
/// modifiers apply. Heat and sensor modifiers are never applied.
/// </summary>
public class PhysicalAttackCalculator : IPhysicalAttackCalculator
{
    private readonly IRulesProvider _rules;

    public PhysicalAttackCalculator(IRulesProvider rules)
    {
        _rules = rules;
    }

    public int GetToHitNumber(IUnit attacker, IUnit target, PhysicalAttackType attackType, IBattleMap map)
    {
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, attackType);
        return GetModifierBreakdown(scenario, map).Total;
    }

    public PhysicalAttackBreakdown GetModifierBreakdown(IUnit attacker, IUnit target, PhysicalAttackType attackType, IBattleMap map)
    {
        var scenario = PhysicalAttackScenario.FromUnits(attacker, target, attackType);
        return GetModifierBreakdown(scenario, map);
    }

    public int GetToHitNumber(PhysicalAttackScenario scenario, IBattleMap map)
    {
        return GetModifierBreakdown(scenario, map).Total;
    }

    public PhysicalAttackBreakdown GetModifierBreakdown(PhysicalAttackScenario scenario, IBattleMap map)
    {
        var losResult = map.GetLineOfSight(
            scenario.AttackerPosition.Coordinates,
            scenario.TargetPosition.Coordinates,
            scenario.AttackerHeight,
            scenario.TargetHeight,
            scenario.AttackerPosition.Surface,
            scenario.TargetPosition.Surface);

        var terrainModifiers = TerrainModifierHelper.GetTerrainModifiers(losResult, _rules);

        return new PhysicalAttackBreakdown
        {
            PilotingBase = new PilotingRollModifier
            {
                Value = scenario.AttackerPiloting
            },
            AttackTypeModifier = new PhysicalAttackBaseModifier
            {
                Value = _rules.GetPhysicalAttackBaseModifier(scenario.AttackType),
                AttackType = scenario.AttackType
            },
            AttackerMovement = new AttackerMovementModifier
            {
                Value = _rules.GetAttackerMovementModifier(scenario.AttackerMovementType),
                MovementType = scenario.AttackerMovementType
            },
            TargetMovement = new TargetMovementModifier
            {
                Value = _rules.GetTargetMovementModifier(scenario.TargetHexesMoved),
                HexesMoved = scenario.TargetHexesMoved
            },
            OtherModifiers = [],
            TerrainModifiers = terrainModifiers
        };
    }
}
