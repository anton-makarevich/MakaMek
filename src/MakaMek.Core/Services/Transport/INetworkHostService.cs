using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Interface for services that manage network hosts for multiplayer
/// </summary>
public interface INetworkHostService : IDisposable
{
    /// <summary>
    /// Gets the transport publisher associated with this host
    /// </summary>
    ITransportPublisher? Publisher { get; }
    
    /// <summary>
    /// Gets the full hub URL for clients to connect to
    /// </summary>
    string? HubUrl { get; }
    
    /// <summary>
    /// Gets a value indicating whether the host is running
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// Starts the network host on the specified port
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task Start();
    
    /// <summary>
    /// Stops the network host if it is running
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task Stop();
    
    /// <summary>
    /// Gets a value indicating whether the host can be started
    /// </summary>
    bool CanStart { get; }
}
