using Sanet.MakaMek.Core.Data.Game.Mechanics;
using Sanet.MakaMek.Core.Data.Game.Mechanics.PilotingSkillRollContexts;
using Sanet.MakaMek.Core.Models.Units;

namespace Sanet.MakaMek.Core.Models.Game.Mechanics.Mechs.Falling;

/// <summary>
/// Interface for calculating piloting skill roll target numbers
/// </summary>
public interface IPilotingSkillCalculator
{
    /// <summary>
    /// Gets a detailed breakdown of all modifiers affecting the piloting skill roll.
    /// The context object carries the roll type and any additional data (e.g. water depth, levels fallen).
    /// </summary>
    /// <param name="unit">The unit making the piloting skill roll.</param>
    /// <param name="context">The piloting skill roll context, including roll type and optional extra data.</param>
    /// <param name="game">The game instance, used for accessing the map and other game state.</param>
    /// <returns>A breakdown of the piloting skill roll calculation</returns>
    PsrBreakdown GetPsrBreakdown(Unit unit, PilotingSkillRollContext context, IGame? game = null);

    /// <summary>
    /// Evaluates a piloting skill roll and returns complete roll data.
    /// </summary>
    /// <param name="psrBreakdown">The PSR breakdown containing target number and modifiers.</param>
    /// <param name="unit">The unit making the piloting skill roll.</param>
    /// <param name="context">The piloting skill roll context, including roll type and optional extra data.</param>
    /// <returns>Complete piloting skill roll data including dice results.</returns>
    PilotingSkillRollData EvaluateRoll(PsrBreakdown psrBreakdown, Unit unit, PilotingSkillRollContext context);
}
