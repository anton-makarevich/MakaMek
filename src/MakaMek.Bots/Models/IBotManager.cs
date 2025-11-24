using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.Models;

/// <summary>
/// Manages the lifecycle of bot players in the game
/// </summary>
public interface IBotManager
{
    /// <summary>
    /// Gets the list of active bots
    /// </summary>
    IReadOnlyList<IBot> Bots { get; }

    /// <summary>
    /// Initializes the bot manager with a client game instance
    /// </summary>
    /// <param name="clientGame">The client game to manage bots for</param>
    void Initialize(IClientGame clientGame);

    /// <summary>
    /// Adds a new bot player to the game
    /// </summary>
    /// <param name="player">The player to add as a bot</param>
    void AddBot(IPlayer player);

    /// <summary>
    /// Removes a bot player from the game
    /// </summary>
    /// <param name="playerId">The ID of the bot player to remove</param>
    void RemoveBot(Guid playerId);

    /// <summary>
    /// Removes all bots and cleans up resources
    /// </summary>
    void Clear();

    /// <summary>
    /// Checks if a given player is controlled by a bot
    /// </summary>
    /// <param name="playerId">The ID of the player to check</param>
    /// <returns>True if the player is a bot, false otherwise</returns>
    bool IsBot(Guid playerId);

    IDecisionEngineProvider? DecisionEngineProvider { get; }
}

