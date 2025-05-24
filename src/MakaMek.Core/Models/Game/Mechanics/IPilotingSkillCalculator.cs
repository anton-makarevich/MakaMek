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
    /// <param name="rollTypes">A collection of specific Piloting Skill Roll types to consider.</param>
    /// <param name="map">A map for location context</param>
    /// <returns>A breakdown of the piloting skill roll calculation.</returns>
    PsrBreakdown GetPsrBreakdown(Unit unit, IEnumerable<PilotingSkillRollType> rollTypes, BattleMap? map = null);
}
