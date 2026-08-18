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
    /// <param name="commandPublisher">The command publisher to use for sending/receiving commands.</param>
    /// <param name="serverGameId">When set, the client will only process commands whose
    /// <see cref="IGameCommand.GameOriginId"/> matches this server game id — i.e. commands
    /// that were validated and rebroadcast by the authoritative server.</param>
    ClientGame CreateClientGame(ICommandPublisher commandPublisher, Guid? serverGameId = null);
}
