using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Components.Internal.Actuators;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

/// <summary>
/// Classic BattleTech implementation of piloting skill roll calculator
/// </summary>
public class PilotingSkillCalculator : IPilotingSkillCalculator
{
    private readonly IRulesProvider _rules;
    public PilotingSkillCalculator(IRulesProvider rules)
    {
        _rules = rules;
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
        if (unit.Crew == null)
        {
            throw new ArgumentException("Unit has no crew", nameof(unit));
        }

        if (unit is not Mech mech)
            return new PsrBreakdown
            {
                BasePilotingSkill = unit.Crew.Piloting,
                Modifiers = new List<RollModifier>()
            };

        var modifiers = new List<RollModifier>();
        
        // Add standard modifiers
        modifiers.AddRange(GetStandardModifiers(mech, game));
        
        // Add special modifiers for specific roll types
        modifiers.AddRange(GetModifiersForRoll(rollType, mech, game));

        return new PsrBreakdown
        {
            BasePilotingSkill = unit.Crew.Piloting,
            Modifiers = modifiers
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
        if (gyroHits > 0)
        {
            modifiers.Add(new DamagedGyroModifier
            {
                Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit),
                HitsCount = gyroHits
            });
        }

        // Lower leg actuator hit modifier
        var lowerLegActuators = mech.GetAllComponents<LowerLegActuator>();
        foreach (var actuator in lowerLegActuators)
        {
            if (actuator.IsDestroyed)
            {
                modifiers.Add(new LowerLegActuatorHitModifier
                {
                    Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.LowerLegActuatorHit)
                });
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
            var levelsFallen = 0; // TODO: Calculate levels fallen if game with map is provided
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
