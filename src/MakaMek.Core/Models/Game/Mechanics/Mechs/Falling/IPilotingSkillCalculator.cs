using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

/// <summary>
/// Interface for calculating piloting skill roll target numbers
/// </summary>
public interface IPilotingSkillCalculator
{
    /// <summary>
    /// Gets a detailed breakdown of all modifiers affecting the piloting skill roll.
    /// </summary>
    /// <param name="unit">The unit making the piloting skill roll.</param>
    /// <param name="rollType">The specific Piloting Skill Roll type to consider.</param>
    /// <param name="game">The game instance, used for accessing the map and other game state.</param>
    /// <param name="totalDamage">The total damage taken by the unit, used specifically for the HeavyDamage modifier check.</param>
    /// <returns>A breakdown of the piloting skill roll calculation</returns>
    PsrBreakdown GetPsrBreakdown(Unit unit, PilotingSkillRollType rollType, IGame? game = null, int totalDamage = 0);
}
