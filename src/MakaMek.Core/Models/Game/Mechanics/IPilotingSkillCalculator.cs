using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics;

/// <summary>
/// Interface for calculating piloting skill roll target numbers
/// </summary>
public interface IPilotingSkillCalculator
{
    /// <summary>
    /// Gets a detailed breakdown of all modifiers affecting the piloting skill roll.
    /// </summary>
    /// <param name="unit">The unit making the piloting skill roll.</param>
    /// <param name="rollTypes">A collection of specific Piloting Skill Roll types to consider. If null or empty, all relevant modifiers are calculated.</param>
    /// <param name="map">The battle map, used for terrain-based modifiers</param>
    /// <param name="totalDamage">The total damage taken by the unit, used specifically for the HeavyDamage modifier check.</param>
    /// <returns>A breakdown of the piloting skill roll calculation</returns>
    PsrBreakdown GetPsrBreakdown(Unit unit, IEnumerable<PilotingSkillRollType> rollTypes, BattleMap? map = null, int totalDamage = 0);
}
