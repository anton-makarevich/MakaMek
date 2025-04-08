using Sanet.Transport;

namespace Sanet.MakaMek.Core.Models.Game.Factories;

/// <summary>
/// Factory interface for creating transport publishers
/// </summary>
public interface ITransportFactory
{
    /// <summary>
    /// Creates a network client transport publisher for connecting to a server
    /// </summary>
    /// <param name="serverAddress">The address of the server to connect to</param>
    /// <returns>A transport publisher connected to the server</returns>
    Task<ITransportPublisher> CreateAndStartClientPublisher(string serverAddress);
}
