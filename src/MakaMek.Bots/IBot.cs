using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots;

/// <summary>
/// Represents a bot player that can make automated decisions in the game
/// </summary>
public interface IBot : IDisposable
{
    /// <summary>
    /// Gets the player associated with this bot
    /// </summary>
    IPlayer Player { get; }
    
    /// <summary>
    /// Gets the difficulty level of this bot
    /// </summary>
    BotDifficulty Difficulty { get; }
}

