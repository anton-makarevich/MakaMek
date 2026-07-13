using Sanet.MakaMek.Core.Services.Transport;

namespace Sanet.MakaMek.Core.Models.Game.Factories;

/// <summary>
/// Factory interface for creating game instances.
/// </summary>
public interface IGameFactory
{
    /// <summary>
    /// Creates a new server-side game instance.
    /// </summary>
    ServerGame CreateServerGame(ICommandPublisher commandPublisher);

    /// <summary>
    /// Creates a new client-side game instance.
    /// </summary>
    ClientGame CreateClientGame(ICommandPublisher commandPublisher);
}
