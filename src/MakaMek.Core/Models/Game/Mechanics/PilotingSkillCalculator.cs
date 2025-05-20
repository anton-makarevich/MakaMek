using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers;
using Sanet.MakaMek.Core.Models.Game.Mechanics.Modifiers.PilotingSkill;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Core.Models.Units.Components.Internal;
using Sanet.MakaMek.Core.Models.Units.Mechs;
using Sanet.MakaMek.Core.Utils.TechRules;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

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
    /// Gets a detailed breakdown of all modifiers affecting the piloting skill roll
    /// </summary>
    /// <param name="unit">The unit making the piloting skill roll</param>
    /// <param name="rollTypes">An optional collection of specific Piloting Skill Roll types to consider. If null or empty, all applicable modifiers are calculated.</param>
    /// <returns>A breakdown of the piloting skill roll calculation</returns>
    public PsrBreakdown GetPsrBreakdown(Unit unit, IEnumerable<PilotingSkillRollType>? rollTypes = null)
    {
        if (unit.Crew == null)
        {
            throw new ArgumentException("Unit has no crew", nameof(unit));
        }

        var modifiers = new List<RollModifier>();
        var relevantRollTypes = rollTypes?.ToList() ?? [];

        // Determine if we need to calculate all modifiers or specific ones
        var calculateAll = relevantRollTypes.Count == 0;

        // Add damaged gyro modifier if applicable
        if (calculateAll || relevantRollTypes.Contains(PilotingSkillRollType.GyroHit))
        {
            if (unit is Mech mech)
            {
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
            }
        }
        
        // Future PSR modifiers can be added here following the same pattern:
        // if (calculateAll || relevantRollTypes.Contains(PilotingSkillRollType.SomeOtherCondition))
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
