using Sanet.MakaMek.Bots.Data;

namespace Sanet.MakaMek.Bots.Models;

/// <summary>
/// Represents a bot player that can make automated decisions in the game
/// </summary>
public interface IBot : IDisposable
{
    /// <summary>
    /// Gets the ID of the player associated with this bot
    /// </summary>
    Guid PlayerId { get; }

    /// <summary>
    /// Gets the settings that control this bot's behavior
    /// </summary>
    BotSettings Settings { get; }
}