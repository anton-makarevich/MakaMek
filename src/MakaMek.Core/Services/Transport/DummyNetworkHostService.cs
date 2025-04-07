using Sanet.Transport;

namespace Sanet.MakaMek.Core.Services.Transport;

/// <summary>
/// Dummy implementation of INetworkHostService for platforms that don't support SignalR hosting
/// </summary>
public class DummyNetworkHostService : INetworkHostService
{
    /// <summary>
    /// Always returns null as hosting is not supported
    /// </summary>
    public ITransportPublisher? Publisher => null;
    
    /// <summary>
    /// Always returns null as hosting is not supported
    /// </summary>
    public string? HubUrl => null;
    
    /// <summary>
    /// Always returns false as hosting is not supported
    /// </summary>
    public bool IsRunning => false;
    
    /// <summary>
    /// No-op implementation that returns a completed task
    /// </summary>
    public Task Start()
    {
        // No-op implementation
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// No-op implementation
    /// </summary>
    public Task Stop()
    {
        // No-op implementation
        return Task.CompletedTask;
    }

    public bool CanStart => false;

    /// <summary>
    /// No-op implementation
    /// </summary>
    public void Dispose()
    {
        // No-op implementation
    }
}
