using Sanet.Transport;
using Sanet.Transport.SignalR.Client.Publishers;

namespace Sanet.MakaMek.Core.Models.Game.Factories;

/// <summary>
/// Implementation of ITransportFactory that creates SignalR transport publishers
/// </summary>
public class SignalRTransportFactory : ITransportFactory
{
    /// <summary>
    /// Creates a SignalR client transport publisher for connecting to a server
    /// </summary>
    /// <param name="serverAddress">The address of the server to connect to</param>
    /// <returns>A SignalR transport publisher connected to the server</returns>
    public async Task<ITransportPublisher> CreateAndStartClientPublisher(string serverAddress)
    {
        var client = new SignalRClientPublisher(serverAddress);
        await client.StartAsync();
        return client;
    }
}
