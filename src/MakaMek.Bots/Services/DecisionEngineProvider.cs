using Sanet.MakaMek.Bots.DecisionEngines;
using Sanet.MakaMek.Core.Models.Game.Phases;

namespace Sanet.MakaMek.Bots.Services;

/// <summary>
/// Provides decision engines for specific game phases
/// </summary>
public class DecisionEngineProvider : IDecisionEngineProvider
{
    private readonly Dictionary<PhaseNames, IBotDecisionEngine> _decisionEngines;

    public DecisionEngineProvider(Dictionary<PhaseNames, IBotDecisionEngine> decisionEngines)
    {
        _decisionEngines = decisionEngines;
    }

    public IBotDecisionEngine? GetEngineForPhase(PhaseNames phase)
    {
        return _decisionEngines.GetValueOrDefault(phase);
    }
}
