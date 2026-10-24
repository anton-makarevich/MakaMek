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
/// <para>
/// The attacker movement rows of the Attack Modifiers Table are exclusive states:
/// a prone (+2) or skidding (+1) attacker-state modifier replaces the movement-type
/// modifier instead of stacking on it, and prone wins over skidding
/// (see <see cref="AttackerStateModifierResolver"/>).
/// </para>
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

    /// <summary>
    /// Builds the modifier breakdown for a physical attack scenario.
    /// Attacker-state modifiers (prone +2 / skidding +1) are exclusive attacker movement
    /// rows: they replace the movement-type modifier, and prone wins over skidding.
    /// </summary>
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

        // Attacker-state modifiers (prone/skidding) are exclusive attacker movement rows:
        // the prone (+2) or skidding (+1) modifier takes the place of the movement-type
        // modifier (Attack Modifiers Table); prone wins over skidding.
        var attackerStateModifier = AttackerStateModifierResolver.Resolve(
            scenario.AttackerUnit?.IsProne == true,
            scenario.AttackerUnit?.IsSkidding == true);
        if (attackerStateModifier != null)
        {
            otherModifiers.Add(attackerStateModifier);
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
                Value = attackerStateModifier != null
                    ? 0
                    : _rules.GetAttackerMovementModifier(scenario.AttackerMovementType),
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

        if (scenario.TargetUnit.Status.HasFlag(UnitStatus.Prone))
        {
            var distance = scenario.AttackerPosition.Coordinates.DistanceTo(scenario.TargetPosition.Coordinates);
            modifiers.Add(new ProneTargetModifier { Value = _rules.GetProneTargetModifier(distance) });
        }

        return modifiers;
    }
}
