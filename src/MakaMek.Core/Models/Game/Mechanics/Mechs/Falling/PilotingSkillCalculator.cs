using Sanet.MakaMek.Core.Data.Game.Mechanics;
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
    /// Gets a detailed breakdown of all modifiers affecting the piloting skill roll with additional context
    /// </summary>
    /// <param name="unit">The unit making the piloting skill roll</param>
    /// <param name="rollType">The specific Piloting Skill Roll type to consider</param>
    /// <param name="game">The game instance, used for accessing the map and other game state</param>
    /// <returns>A breakdown of the piloting skill roll calculation</returns>
    public PsrBreakdown GetPsrBreakdown(
        Unit unit, 
        PilotingSkillRollType rollType,
        IGame? game = null)
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
        modifiers.AddRange(GetStandardModifiers(mech, game));
        
        // Add special modifiers for specific roll types
        modifiers.AddRange(GetModifiersForRoll(rollType, mech, game));

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
    /// <param name="rollType">The type of piloting skill roll</param>
    /// <returns>Complete piloting skill roll data including dice results</returns>
    public PilotingSkillRollData EvaluateRoll(PsrBreakdown psrBreakdown, Unit unit, PilotingSkillRollType rollType)
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
                RollType = rollType,
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
            RollType = rollType,
            DiceResults = diceResults.Select(d => d.Result).ToArray(),
            IsSuccessful = isSuccessful,
            PsrBreakdown = psrBreakdown
        };
    }

    /// <summary>
    /// Gets all standard modifiers that apply to piloting skill rolls
    /// </summary>
    /// <param name="mech">The mech making the piloting skill roll</param>
    /// <param name="game">The game instance, used for accessing the map and other game state</param>
    /// <returns>A collection of standard roll modifiers</returns>
    private IEnumerable<RollModifier> GetStandardModifiers(Mech mech, IGame? game = null)
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
        if (mech.TotalPhaseDamage > _rules.GetHeavyDamageThreshold())
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
    /// Gets special modifiers that apply only to specific roll types
    /// </summary>
    /// <param name="rollType">The specific roll type</param>
    /// <param name="mech">The mech making the piloting skill roll</param>
    /// <param name="game">The game instance, used for accessing the map and other game state</param>
    /// <returns>A collection of special roll modifiers for the specified roll type</returns>
    private IEnumerable<RollModifier> GetModifiersForRoll(PilotingSkillRollType rollType, Mech mech, IGame? game = null)
    {
        var modifiers = new List<RollModifier>();
        if (rollType == PilotingSkillRollType.PilotDamageFromFall)
        {
            // Falling levels modifier
            const int levelsFallen = 0; // TODO: Calculate levels fallen if game with map is provided
            modifiers.Add(new FallingLevelsModifier
            {
                Value = Math.Max(0, levelsFallen - 1), // +1 for each level above 1
                LevelsFallen = levelsFallen
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
