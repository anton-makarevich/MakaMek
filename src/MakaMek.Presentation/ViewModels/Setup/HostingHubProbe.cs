using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Presentation.Models.Logger;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Resolves the active relay hub row for the online hosting screen and probes its reachability. Kept
/// out of the hosting view model so it does not depend on the hub catalogue or the relay client.
/// </summary>
public sealed class HostingHubProbe
{
    private readonly IRelayHubConfigurationProvider _hubConfigurationProvider;
    private readonly IRelayRoomClient _relayRoomClient;
    private readonly ILogger _logger;

    public HostingHubProbe(
        IRelayHubConfigurationProvider hubConfigurationProvider,
        IRelayRoomClient relayRoomClient,
        ILogger<HostingHubProbe> logger)
    {
        _hubConfigurationProvider = hubConfigurationProvider;
        _relayRoomClient = relayRoomClient;
        _logger = logger;
    }

    /// <summary>
    /// Reads the active hub row. Returns <c>null</c> when hosting switched away from online (or the
    /// screen was disposed) while the reads were in flight, so a stale resolution never resurrects the
    /// hub row. The caller applies the row and probes its status so the badge settles in place.
    /// </summary>
    public async Task<HubEntryViewModel?> ResolveActiveHubAsync(
        CancellationToken cancellationToken,
        Func<bool> isOnlineMode,
        Func<bool> isDisposed)
    {
        var hubs = await _hubConfigurationProvider.GetHubs();
        if (cancellationToken.IsCancellationRequested || isDisposed() || !isOnlineMode())
            return null;

        var activeHubId = await _hubConfigurationProvider.GetActiveHubId();
        if (cancellationToken.IsCancellationRequested || isDisposed() || !isOnlineMode())
            return null;

        var activeHub = hubs.FirstOrDefault(h => h.Id == activeHubId);
        return activeHub == null
            ? null
            : new HubEntryViewModel(activeHub, isNew: false, checkStatus: CheckHubStatusAsync);
    }

    private async Task<HubStatus> CheckHubStatusAsync(HubEntryViewModel entry, CancellationToken cancellationToken)
    {
        try
        {
            var options = new RelayClientOptions
            {
                BaseUrl = entry.BaseUrl,
                ApiKey = entry.ApiKey
            };
            var error = await _relayRoomClient.Health(cancellationToken, options);
            return error == null ? HubStatus.Online : HubStatus.Offline;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogHubStatusProbeFailed(entry.Name, ex);
            return HubStatus.Offline;
        }
    }
}
