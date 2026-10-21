using Sanet.MakaMek.Core.Data.Game.Mechanics.PhysicalAttacks;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Attack;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.Physical;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.PhysicalAttack;

/// <summary>
/// Classic BattleTech implementation of the physical attack to-hit calculator.
/// Base to-hit is the attacker's Piloting Skill plus a per-type base modifier
/// (Punch +0, Kick -2); standard attacker movement, target movement and terrain
/// modifiers apply, as do target-side modifiers (partial cover, skidding) and the
/// attacker-side skidding modifier. Heat and sensor modifiers are never applied.
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

        var terrainModifiers = losResult.GetTerrainToHitModifiers(_rules);

        var otherModifiers = GetTargetModifiers(scenario, losResult);

        // A skidding attacker replaced its normal movement with the skid, so the skidding
        // modifier takes the place of the movement-type modifier (Attack Modifiers Table).
        var isSkidding = scenario.AttackerUnit?.IsSkidding == true;
        if (isSkidding)
        {
            otherModifiers.Add(new SkiddingAttackerModifier
            {
                Value = SkiddingAttackerModifier.DefaultValue
            });
        }

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
                Value = isSkidding ? 0 : _rules.GetAttackerMovementModifier(scenario.AttackerMovementType),
                MovementType = scenario.AttackerMovementType
            },
            TargetMovement = new TargetMovementModifier
            {
                Value = _rules.GetTargetMovementModifier(scenario.TargetHexesMoved),
                HexesMoved = scenario.TargetHexesMoved
            },
            OtherModifiers = otherModifiers,
            TerrainModifiers = terrainModifiers
        };
    }

    /// <summary>
    /// Collects the target-side modifiers that apply to physical attacks.
    /// Partial cover (+1) and skidding targets (+2) apply to all attacks,
    /// weapon and physical alike.
    /// </summary>
    private List<RollModifier> GetTargetModifiers(
        PhysicalAttackScenario scenario,
        LineOfSightResult losResult)
    {
        List<RollModifier> modifiers = [];

        if (scenario.TargetUnit is null) return modifiers;

        if (_rules.HasPartialCover(scenario.TargetUnit, losResult))
        {
            modifiers.Add(new PartialCoverModifier
            {
                Value = _rules.GetPartialCoverModifier()
            });
        }

        if (scenario.TargetUnit.IsSkidding)
        {
            modifiers.Add(new SkiddingTargetModifier
            {
                Value = SkiddingTargetModifier.DefaultValue
            });
        }

        return modifiers;
    }
}
