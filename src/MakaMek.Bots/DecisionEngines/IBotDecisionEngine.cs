namespace Sanet.MakaMek.Bots.DecisionEngines;

/// <summary>
/// Represents a decision engine that makes decisions for a bot during a specific game phase
/// </summary>
public interface IBotDecisionEngine
{
    /// <summary>
    /// Makes a decision for the bot based on the current game state
    /// </summary>
    /// <returns>A task that completes when the decision has been made and executed</returns>
    Task MakeDecision();
}

