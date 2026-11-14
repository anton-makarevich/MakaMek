using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Services.Transport;
using Sanet.Transport;

namespace Sanet.MakaMek.Core.Models.Game;

/// <summary>
/// Starts and stops the LAN host of <see cref="GameManager"/> and keeps the host's publisher
/// installed on the transport adapter. Extracted from the manager so it only orchestrates the
/// lobby and server game lifecycle.
/// </summary>
internal sealed class LanHostController(
    ICommandPublisher commandPublisher,
    INetworkHostService? networkHostService,
    ILogger logger)
{
    private ITransportPublisher? _lanPublisher;

    public bool IsRunning => networkHostService?.IsRunning ?? false;

    public bool CanStart => networkHostService?.CanStart ?? false;

    public string? HubUrl => networkHostService?.HubUrl;

    /// <summary>
    /// Starts the network host if supported and not already running, and installs its publisher
    /// on the transport adapter. A cancellation after the host started stops it again so no
    /// orphaned LAN server is left running.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!CanStart || IsRunning || networkHostService == null) return;

        await networkHostService.Start();

        if (cancellationToken.IsCancellationRequested)
        {
            // The host started after cancellation was requested; stop it
            // again so no orphaned LAN server is left running.
            await StopAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }

        // Add the network publisher to the transport adapter if successfully started
        if (networkHostService.Publisher == null) return;

        try
        {
            commandPublisher.Adapter.AddPublisher(networkHostService.Publisher);
            _lanPublisher = networkHostService.Publisher;

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            // Cancellation occurred after the publisher was registered;
            // remove it and stop the host so nothing is left orphaned.
            await StopAsync();
            throw;
        }
    }

    /// <summary>
    /// Removes the LAN publisher from the transport adapter and stops the host if it is running.
    /// </summary>
    public async Task StopAsync()
    {
        RemovePublisher();

        if (networkHostService is not { IsRunning: true }) return;

        try
        {
            await networkHostService.Stop();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to stop the network host service");
        }
    }

    /// <summary>
    /// Removes the LAN publisher from the transport adapter without stopping the host.
    /// </summary>
    public void RemovePublisher()
    {
        if (_lanPublisher == null) return;

        try
        {
            commandPublisher.Adapter.RemovePublisher(_lanPublisher);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to remove LAN publisher from the transport adapter");
        }
        _lanPublisher = null;
    }

    public void DisposeHost() => networkHostService?.Dispose();

    public async ValueTask DisposeHostAsync()
    {
        if (networkHostService != null)
            await networkHostService.DisposeAsync();
    }
}
