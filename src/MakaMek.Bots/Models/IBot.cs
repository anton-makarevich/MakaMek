using Sanet.MakaMek.Core.Models.Game.Players;

namespace Sanet.MakaMek.Bots.Models;

/// <summary>
/// Represents a bot player that can make automated decisions in the game
/// </summary>
public interface IBot : IDisposable
{
    /// <summary>
    /// Gets the player associated with this bot
    /// </summary>
    IPlayer Player { get; }
}