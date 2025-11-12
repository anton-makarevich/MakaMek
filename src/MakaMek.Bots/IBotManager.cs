using Sanet.MakaMek.Core.Models.Game;
using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots;

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
    void Initialize(ClientGame clientGame);

    /// <summary>
    /// Adds a new bot player to the game
    /// </summary>
    /// <param name="player">The player to add as a bot</param>
    /// <param name="difficulty">The difficulty level of the bot</param>
    void AddBot(IPlayer player, BotDifficulty difficulty = BotDifficulty.Easy);

    /// <summary>
    /// Removes a bot player from the game
    /// </summary>
    /// <param name="playerId">The ID of the bot player to remove</param>
    void RemoveBot(Guid playerId);

    /// <summary>
    /// Removes all bots and cleans up resources
    /// </summary>
    void Clear();
}

