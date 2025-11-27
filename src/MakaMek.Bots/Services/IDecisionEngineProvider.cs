using Sanet.MakaMek.Bots.Models.DecisionEngines;
using Sanet.MakaMek.Core.Models.Game.Phases;

namespace Sanet.MakaMek.Bots.Services;

/// <summary>
/// Provides decision engines for specific game phases
/// </summary>
public interface IDecisionEngineProvider
{
    /// <summary>
    /// Gets the decision engine for the specified phase
    /// </summary>
    /// <param name="phase">The game phase</param>
    /// <returns>The decision engine for the phase, or null if no engine exists for the phase</returns>
    IBotDecisionEngine? GetEngineForPhase(PhaseNames phase);
}
