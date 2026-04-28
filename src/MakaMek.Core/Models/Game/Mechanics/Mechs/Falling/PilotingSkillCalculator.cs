using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Game.Dice;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Models.Game.Rules;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Mechs;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

/// <summary>
/// Classic BattleTech implementation of piloting skill roll calculator
/// </summary>
public class PilotingSkillCalculator : IPilotingSkillCalculator
{
    private readonly IRulesProvider _rules;
    private readonly IDiceRoller _diceRoller;

    public PilotingSkillCalculator(IRulesProvider rules, IDiceRoller diceRoller)
    {
        _rules = rules;
        _diceRoller = diceRoller;
    }
    
    /// <summary>
    /// Gets a detailed breakdown of all modifiers affecting the piloting skill roll.
    /// The context carries the roll type and any extra data (e.g. water depth, levels fallen).
    /// </summary>
    /// <param name="unit">The unit making the piloting skill roll</param>
    /// <param name="context">The piloting skill roll context</param>
    /// <param name="game">The game instance, used for accessing the map and other game state</param>
    /// <returns>A breakdown of the piloting skill roll calculation</returns>
    public PsrBreakdown GetPsrBreakdown(
        Unit unit,
        PilotingSkillRollContext context,
        IGame? game = null)
        => BuildPsrBreakdown(unit, context);

    private PsrBreakdown BuildPsrBreakdown(Unit unit, PilotingSkillRollContext context)
    {
        if (unit.Pilot == null)
        {
            throw new ArgumentException("Unit has no pilot", nameof(unit));
        }

        if (unit is not Mech mech)
            return new PsrBreakdown
            {
                BasePilotingSkill = unit.Pilot.Piloting,
                Modifiers = new List<RollModifier>()
            };

        var modifiers = new List<RollModifier>();

        // Add standard modifiers
        modifiers.AddRange(GetStandardModifiers(mech));

        // Add special modifiers derived from the context
        modifiers.AddRange(GetModifiersForContext(context));

        return new PsrBreakdown
        {
            BasePilotingSkill = unit.Pilot.Piloting,
            Modifiers = modifiers
        };
    }

    /// <summary>
    /// Evaluates a piloting skill roll and returns complete roll data
    /// </summary>
    /// <param name="psrBreakdown">The PSR breakdown containing target number and modifiers</param>
    /// <param name="unit">The unit making the piloting skill roll</param>
    /// <param name="context">The piloting skill roll context</param>
    /// <returns>Complete piloting skill roll data including dice results</returns>
    public PilotingSkillRollData EvaluateRoll(PsrBreakdown psrBreakdown, Unit unit, PilotingSkillRollContext context)
    {
        if (unit.Pilot == null)
        {
            throw new ArgumentException("Unit has no pilot", nameof(unit));
        }

        // For unconscious pilots, create automatic failure data without rolling dice
        if (!unit.Pilot.IsConscious)
        {
            return new PilotingSkillRollData
            {
                RollContext = context,
                DiceResults = [],
                IsSuccessful = false,
                PsrBreakdown = psrBreakdown
            };
        }

        // Perform 2d6 dice roll
        var diceResults = _diceRoller.Roll2D6();
        var rollTotal = diceResults.Sum(d => d.Result);
        var isSuccessful = rollTotal >= psrBreakdown.ModifiedPilotingSkill;

        return new PilotingSkillRollData
        {
            RollContext = context,
            DiceResults = diceResults.Select(d => d.Result).ToArray(),
            IsSuccessful = isSuccessful,
            PsrBreakdown = psrBreakdown
        };
    }

    /// <summary>
    /// Gets all standard modifiers that apply to piloting skill rolls
    /// </summary>
    /// <param name="mech">The mech making the piloting skill roll</param>
    /// <returns>A collection of standard roll modifiers</returns>
    private IEnumerable<RollModifier> GetStandardModifiers(Mech mech)
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
    /// Gets special modifiers derived from the roll context (e.g. water depth, levels fallen).
    /// </summary>
    /// <param name="context">The piloting skill roll context</param>
    /// <returns>A collection of special roll modifiers for the given context</returns>
    private IEnumerable<RollModifier> GetModifiersForContext(PilotingSkillRollContext context)
    {
        var modifiers = new List<RollModifier>();
        if (context is PilotDamageFromFallRollContext pilotDamageCtx)
        {
            var levelsFallen = pilotDamageCtx.LevelsFallen;
            modifiers.Add(new FallingLevelsModifier
            {
                Value = Math.Max(0, levelsFallen - 1), // TODO move to rules provider
                LevelsFallen = levelsFallen
            }); 
        }
        else if (context is EnteringDeepWaterRollContext waterCtx && waterCtx.WaterDepth > 0)
        {
            // Water depth modifier: depth 1 = -1, depth 2 = 0, depth 3+ = +1
            modifiers.Add(new WaterDepthModifier
            {
                Value = _rules.GetWaterDepthModifier(waterCtx.WaterDepth),
                WaterDepth = waterCtx.WaterDepth
            });
        }
        return modifiers;
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
