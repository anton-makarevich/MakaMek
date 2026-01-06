using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.Models.DecisionEngines;

/// <summary>
/// Represents a decision engine that makes decisions for a bot during a specific game phase
/// </summary>
public interface IBotDecisionEngine
{
    /// <summary>
    /// Makes a decision for the bot based on the current game state
    /// </summary>
    /// <param name="player">The player for whom to make the decision</param>
    /// <param name="turnState">The turn state for caching data across phases</param>
    /// <returns>A task that completes when the decision has been made and executed</returns>
    Task MakeDecision(IPlayer player, ITurnState? turnState = null);
}

