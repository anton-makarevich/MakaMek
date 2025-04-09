using Sanet.Transport;
using System.Threading.Tasks;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport.SignalR.Server.Infrastructure;

namespace Sanet.MakaMek.Avalonia.Desktop.Services;

/// <summary>
/// Implementation of INetworkHostService using SignalR for desktop platforms
/// </summary>
/// <summary>
/// Service that manages a SignalR host for LAN multiplayer
/// </summary>
public class SignalRHostService : INetworkHostService
{
    private SignalRHostManager? _hostManager;
    private bool _isDisposed;
    private const int MakaMekPort = 2439;
    private const string MakaMekHubName = "makamekhub";
    
    /// <summary>
    /// Gets the transport publisher associated with this host
    /// </summary>
    public ITransportPublisher? Publisher => _hostManager?.Publisher;
    
    /// <summary>
    /// Gets the full hub URL for clients to connect to
    /// </summary>
    public string? HubUrl => _hostManager?.HubUrl;
    
    /// <summary>
    /// Starts the SignalR host on the specified port
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task Start()
    {
        if (_hostManager != null)
            return; // Already started
            
        _hostManager = new SignalRHostManager(MakaMekPort, MakaMekHubName);
        await _hostManager.Start();
        
        // No need to extract IP address anymore, we'll use the full hub URL
    }
    
    /// <summary>
    /// Gets a value indicating whether the host is running
    /// </summary>
    public bool IsRunning => _hostManager != null;
    
    /// <summary>
    /// Stops the SignalR host if it is running
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public Task Stop()
    {
        if (_hostManager == null) return Task.CompletedTask;
        _hostManager.Dispose();
        _hostManager = null;

        return Task.CompletedTask;
    }

    public bool CanStart => true;

    /// <summary>
    /// Disposes the host service and stops the SignalR host
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        
        _isDisposed = true;
        _hostManager?.Dispose();
        _hostManager = null;
    }
}
