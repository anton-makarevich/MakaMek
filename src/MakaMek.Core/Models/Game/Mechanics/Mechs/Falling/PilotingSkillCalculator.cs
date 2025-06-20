using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
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
    /// <param name="rollTypes">A collection of specific Piloting Skill Roll types to consider. If null or empty, all relevant modifiers are calculated.</param>
    /// <param name="map">The battle map, used for terrain-based modifiers</param>
    /// <param name="totalDamage">The total damage taken by the unit, used specifically for the HeavyDamage modifier check.</param>
    /// <returns>A breakdown of the piloting skill roll calculation</returns>
    public PsrBreakdown GetPsrBreakdown(
        Unit unit, 
        IEnumerable<PilotingSkillRollType> rollTypes,
        BattleMap? map = null, int totalDamage=0)
    {
        if (unit.Crew == null)
        {
            throw new ArgumentException("Unit has no crew", nameof(unit));
        }

        var modifiers = new List<RollModifier>();
        var relevantRollTypes = rollTypes.ToList();
        if (unit is Mech mech)
        {
            // Add damaged gyro modifier if applicable
            if (relevantRollTypes.Contains(PilotingSkillRollType.GyroHit))
            {
                // Check for damaged gyro
                var gyroHits = GetGyroHits(mech);
                if (gyroHits == 1)
                {
                    modifiers.Add(new DamagedGyroModifier
                    {
                        Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.GyroHit),
                        HitsCount = gyroHits
                    });
                }
            }

            // Add Lower Leg Actuator Hit modifier if applicable
            if (relevantRollTypes.Contains(PilotingSkillRollType.LowerLegActuatorHit))
            {
                modifiers.Add(new LowerLegActuatorHitModifier
                {
                    Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.LowerLegActuatorHit)
                });
            }
            
            // Add Heavy Damage modifier if applicable
            if (relevantRollTypes.Contains(PilotingSkillRollType.HeavyDamage))
            {
                modifiers.Add(new HeavyDamageModifier
                {
                    Value = _rules.GetPilotingSkillRollModifier(PilotingSkillRollType.HeavyDamage),
                    DamageTaken = totalDamage 
                });
            }
        }



        // Add MechWarrior damage from fall modifiers if applicable
        if (relevantRollTypes.Contains(PilotingSkillRollType.PilotDamageFromFall))
        {
            // TODO: Calculate levels fallen if map is provided
            var levelsFallen = 0;
            
            // Add modifiers for levels fallen
            // According to the rules, there's a +1 modifier for every level above 1 fallen
            modifiers.Add(new FallingLevelsModifier
            {
                Value = Math.Max(0, levelsFallen - 1), // +1 for each level above 1
                LevelsFallen = levelsFallen
            });
        }
        
        // Future PSR modifiers can be added here following the same pattern:
        // if (relevantRollTypes.Contains(PilotingSkillRollType.SomeOtherCondition))
        // {
        //     // logic to calculate and add SomeOtherConditionModifier
        // }

        return new PsrBreakdown
        {
            BasePilotingSkill = unit.Crew.Piloting,
            Modifiers = modifiers
        };
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
