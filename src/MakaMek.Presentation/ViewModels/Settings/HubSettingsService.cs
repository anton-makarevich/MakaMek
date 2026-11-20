using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using Sanet.Localization;
using Sanet.MakaMek.Presentation.Models.Logger;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.Services;
using Sanet.Transport.SignalR.Client.Relay;

namespace Sanet.MakaMek.Presentation.ViewModels.Settings;

/// <summary>
/// Owns the relay-hub section of the settings screen: reading the persisted hubs, probing their
/// reachability, queueing the active-hub writes and running the add-hub dialog. Keeping this out of
/// <see cref="SettingsViewModel"/> means the view model only binds entry row view models and never
/// touches the hub catalogue or the relay client directly.
/// </summary>
public sealed class HubSettingsService
{
    private readonly IRelayHubConfigurationProvider _hubConfigurationProvider;
    private readonly IRelayRoomClient _relayRoomClient;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<HubSettingsService> _logger;
    private Task? _selectHubTask;

    public HubSettingsService(
        IRelayHubConfigurationProvider hubConfigurationProvider,
        IRelayRoomClient relayRoomClient,
        ILocalizationService localizationService,
        ILogger<HubSettingsService> logger)
    {
        _hubConfigurationProvider = hubConfigurationProvider;
        _relayRoomClient = relayRoomClient;
        _localizationService = localizationService;
        _logger = logger;
    }

    /// <summary>
    /// Reads the persisted hubs and starts a reachability probe for each row. The returned entries are
    /// bound by the caller; probes settle in place so the list renders immediately.
    /// </summary>
    public async Task<HubSettingsSnapshot> LoadAsync(Func<HubEntryViewModel, Task> onSaved)
    {
        var hubs = await _hubConfigurationProvider.GetHubs();
        var activeHubId = await _hubConfigurationProvider.GetActiveHubId();

        var entries = hubs
            .Select(hub => new HubEntryViewModel(
                hub,
                isNew: false,
                onSaved: onSaved,
                checkStatus: CheckHubStatusAsync))
            .ToList();

        foreach (var entry in entries)
        {
            entry.RefreshStatusAsync().SafeFireAndForget(
                ex => _logger.LogErrorRefreshingHubStatus(ex));
        }

        return new HubSettingsSnapshot(entries, activeHubId);
    }

    /// <summary>
    /// Persists the current <paramref name="entry"/> edits before the row commits them.
    /// </summary>
    public async Task SaveAsync(HubEntryViewModel entry)
    {
        var pending = entry.PendingHub;
        await _hubConfigurationProvider.UpdateHub(entry.Id, pending.Name, pending.BaseUrl, pending.ApiKey);
    }

    /// <summary>
    /// Removes a custom hub, ignoring the built-in one.
    /// </summary>
    public async Task RemoveAsync(HubEntryViewModel entry)
    {
        if (entry.IsBuiltIn) return;
        await _hubConfigurationProvider.RemoveHub(entry.Id);
    }

    /// <summary>
    /// Queues the active-hub write so consecutive selections persist in selection order even when an
    /// earlier write is still in flight. A failed write never poisons the queue.
    /// </summary>
    public void Select(string hubId)
    {
        var previous = _selectHubTask;
        var task = SelectChainedAsync(previous, hubId);
        _selectHubTask = task;
        task.SafeFireAndForget();
    }

    private async Task SelectChainedAsync(Task? previous, string hubId)
    {
        if (previous != null)
        {
            try
            {
                await previous;
            }
            catch
            {
                // A failed earlier selection must not block or poison later selections.
            }
        }

        await _hubConfigurationProvider.SelectHub(hubId);
    }

    /// <summary>
    /// Shows the add-hub dialog and persists the result. Returns <c>true</c> when the caller should
    /// reload the list (a hub was confirmed and the write was attempted).
    /// </summary>
    public async Task<bool> AddAsync(INavigationService navigationService)
    {
        var dialog = new AddHubViewModel(_localizationService);
        dialog.SetNavigationService(navigationService);
        var result = await navigationService.ShowViewModelForResultAsync<AddHubViewModel, AddHubResult?>(dialog);

        if (result is null) return false;

        var hub = new HubConfigData(
            Guid.NewGuid().ToString("N"),
            result.Name,
            result.BaseUrl,
            result.ApiKey,
            false);
        try
        {
            await _hubConfigurationProvider.AddHub(hub);
        }
        catch (Exception ex)
        {
            _logger.LogFailedToAddHub(hub.Name, ex);
        }

        return true;
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
        catch (Exception ex)
        {
            _logger.LogHubStatusProbeFailed(entry.Name, ex);
            return HubStatus.Offline;
        }
    }
}

/// <summary>
/// The hub rows and the persisted active-hub id, in one immutable read result.
/// </summary>
public sealed record HubSettingsSnapshot(IReadOnlyList<HubEntryViewModel> Hubs, string? ActiveHubId);
