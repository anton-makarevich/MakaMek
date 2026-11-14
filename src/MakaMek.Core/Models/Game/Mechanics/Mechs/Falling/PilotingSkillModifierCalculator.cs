using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

/// <summary>
/// Builds the individual modifiers that apply to a piloting skill roll: the standard
/// mech-condition modifiers and the modifiers derived from the roll context.
/// Extracted from <see cref="PilotingSkillCalculator"/> so the calculator itself only
/// owns the roll contract (breakdown + dice) and stays below the class-coupling limit.
/// </summary>
public sealed class PilotingSkillModifierCalculator
{
    private readonly IRulesProvider _rules;

    public PilotingSkillModifierCalculator(IRulesProvider rules)
    {
        _rules = rules;
    }

    /// <summary>
    /// Gets all standard modifiers that apply to piloting skill rolls
    /// </summary>
    /// <param name="mech">The mech making the piloting skill roll</param>
    /// <returns>A collection of standard roll modifiers</returns>
    public IEnumerable<RollModifier> GetStandardModifiers(Mech mech)
    {
        var modifiers = new List<RollModifier>();

        // Check for damaged gyro
        var gyroHits = GetGyroHits(mech);
        if (gyroHits >= 1)
        {
            modifiers.Add(new DamagedGyroModifier
            {
                Value = gyroHits == 2
                    ? _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroDestroyed)
                    : _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit),
                HitsCount = gyroHits
            });
        }

        // Process leg-related modifiers
        // For each leg: if destroyed, add only +5 modifier; otherwise add individual component modifiers
        var legLocations = new[] { PartLocation.LeftLeg, PartLocation.RightLeg };
        foreach (var legLocation in legLocations)
        {
            if (mech.Parts.TryGetValue(legLocation, out var legPart) && legPart is Leg leg)
            {
                if (leg.IsDestroyed)
                {
                    // Leg is destroyed - add only the +5 leg destroyed modifier
                    // This replaces all individual component modifiers for this leg
                    modifiers.Add(new LegDestroyedModifier
                    {
                        Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.LegDestroyed)
                    });
                }
                else
                {
                    // Leg is not destroyed - add individual component modifiers for this leg
                    AddLegComponentModifiers(leg, modifiers);
                }
            }
        }

        // Heavy damage modifier
        if (mech.TotalPhaseDamage >= _rules.GetHeavyDamageThreshold())
        {
            modifiers.Add(new HeavyDamageModifier
            {
                Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.HeavyDamage),
                DamageTaken = mech.TotalPhaseDamage
            });
        }

        return modifiers;
    }

    /// <summary>
    /// Gets special modifiers derived from the roll context (e.g., water depth, levels fallen).
    /// </summary>
    /// <param name="context">The piloting skill roll context</param>
    /// <returns>A collection of special roll modifiers for the given context</returns>
    public IEnumerable<RollModifier> GetContextModifiers(PilotingSkillRollContext context)
    {
        var modifiers = new List<RollModifier>();
        switch (context)
        {
            case ChargeImpactRollContext:
                modifiers.Add(new ChargeImpactModifier
                {
                    Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.ChargeImpact)
                });
                break;
            case PilotDamageFromFallRollContext pilotDamageCtx:
            {
                var levelsFallen = pilotDamageCtx.LevelsFallen;
                modifiers.Add(new FallingLevelsModifier
                {
                    Value = Math.Max(0, levelsFallen - 1), // TODO move to rules provider
                    LevelsFallen = levelsFallen
                });
                break;
            }
            case EnteringDeepWaterRollContext { WaterDepth: > 0 } waterCtx:
                AddWaterDepthModifier(waterCtx.WaterDepth, modifiers);
                break;
            case PushHitRollContext { WaterDepth: > 0 } pushWaterCtx:
                // A push into water is not a separate roll: the water-entry modifier applies to the
                // push's own PSR.
                AddWaterDepthModifier(pushWaterCtx.WaterDepth, modifiers);
                break;
            case DominoEffectRollContext { WaterDepth: > 0 } dominoWaterCtx:
                // The same rule applies to a domino displacement into water (Total Warfare p. 151-153).
                AddWaterDepthModifier(dominoWaterCtx.WaterDepth, modifiers);
                break;
            case SkidCheckRollContext skidCtx:
                modifiers.Add(new SkiddingModifier
                {
                    Value = _rules.GetSkidModifier(skidCtx.HexesMoved),
                    HexesMoved = skidCtx.HexesMoved
                });
                break;
        }
        return modifiers;
    }

    /// <summary>
    /// Adds individual component modifiers for a specific leg (hip, upper leg, lower leg, foot)
    /// </summary>
    /// <param name="leg">The leg to check for damaged components</param>
    /// <param name="modifiers">The list to add modifiers to</param>
    private void AddLegComponentModifiers(Leg leg, List<RollModifier> modifiers)
    {
        // Hip actuator hit modifier
        var hipActuator = leg.GetComponent<HipActuator>();
        if (hipActuator?.IsDestroyed == true)
        {
            modifiers.Add(new HipActuatorHitModifier
            {
                Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.HipActuatorHit)
            });
        }

        // Upper leg actuator hit modifier
        var upperLegActuator = leg.GetComponent<UpperLegActuator>();
        if (upperLegActuator?.IsDestroyed == true)
        {
            modifiers.Add(new UpperLegActuatorHitModifier
            {
                Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.UpperLegActuatorHit)
            });
        }

        // Lower leg actuator hit modifier
        var lowerLegActuator = leg.GetComponent<LowerLegActuator>();
        if (lowerLegActuator?.IsDestroyed == true)
        {
            modifiers.Add(new LowerLegActuatorHitModifier
            {
                Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.LowerLegActuatorHit)
            });
        }

        // Foot actuator hit modifier
        var footActuator = leg.GetComponent<FootActuator>();
        if (footActuator?.IsDestroyed == true)
        {
            modifiers.Add(new FootActuatorHitModifier
            {
                Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.FootActuatorHit)
            });
        }
    }

    /// <summary>
    /// Adds the standard water-depth PSR modifier: depth 1 = -1, depth 2 = 0, depth 3+ = +1.
    /// </summary>
    private void AddWaterDepthModifier(int waterDepth, List<RollModifier> modifiers)
    {
        modifiers.Add(new WaterDepthModifier
        {
            Value = _rules.GetWaterDepthModifier(waterDepth),
            WaterDepth = waterDepth
        });
    }

    /// <summary>
    /// Gets the number of hits on the mech's gyro
    /// </summary>
    /// <param name="mech">The mech to check</param>
    /// <returns>The number of hits on the gyro</returns>
    private int GetGyroHits(Mech mech)
    {
        var gyro = mech.GetAllComponents<Gyro>().FirstOrDefault();
        if (gyro == null)
        {
            throw new ArgumentException("No gyro found in mech", nameof(mech));
        }
        return gyro.Hits;
    }
}
