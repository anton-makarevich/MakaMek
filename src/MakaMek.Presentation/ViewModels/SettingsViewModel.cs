using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Assets.Services;
using Sanet.Transport.SignalR.Client.Relay;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly IFileCachingService _fileCachingService;
    private readonly IUnitCachingService _unitCachingService;
    private readonly ITerrainAssetService _terrainAssetService;
    private readonly ILocalizationService _localizationService;
    private readonly IRelayHubConfigurationProvider _hubConfigurationProvider;
    private readonly IRelayRoomClient _relayRoomClient;
    private readonly IAssetProviderConfigurationProvider _assetProviderConfigurationProvider;
    private readonly AssetLoadingViewModel _assetLoadingViewModel;
    private readonly ILogger<SettingsViewModel> _logger;
    private HubEntryViewModel? _selectedHub;
    private Task? _selectHubTask;

    public SettingsViewModel(
        IFileCachingService fileCachingService,
        IUnitCachingService unitCachingService,
        ITerrainAssetService terrainAssetService,
        ILocalizationService localizationService,
        IRelayHubConfigurationProvider hubConfigurationProvider,
        IRelayRoomClient relayRoomClient,
        IAssetProviderConfigurationProvider assetProviderConfigurationProvider,
        AssetLoadingViewModel assetLoadingViewModel,
        ILogger<SettingsViewModel> logger)
    {
        _fileCachingService = fileCachingService;
        _unitCachingService = unitCachingService;
        _terrainAssetService = terrainAssetService;
        _localizationService = localizationService;
        _hubConfigurationProvider = hubConfigurationProvider;
        _relayRoomClient = relayRoomClient;
        _assetProviderConfigurationProvider = assetProviderConfigurationProvider;
        _assetLoadingViewModel = assetLoadingViewModel;
        _logger = logger;

        ClearCacheCommand = new AsyncCommand(ClearCacheAsync);
        AddHubCommand = new AsyncCommand(AddHubAsync);
        RemoveHubCommand = new AsyncCommand<HubEntryViewModel>(RemoveHubAsync);
        RemoveAssetProviderCommand = new AsyncCommand<AssetProviderEntryViewModel>(RemoveAssetProviderAsync);
        AddProviderCommand = new AsyncCommand(AddProviderAsync);
        ReloadProvidersCommand = new AsyncCommand(ReloadProvidersAsync);
    }

    public ICommand ClearCacheCommand { get; }
    public ICommand AddHubCommand { get; }
    public ICommand RemoveHubCommand { get; }
    public ICommand RemoveAssetProviderCommand { get; }
    public ICommand AddProviderCommand { get; }
    public ICommand ReloadProvidersCommand { get; }

    public string CacheStatus
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public ObservableCollection<HubEntryViewModel> Hubs { get; } = [];

    public HubEntryViewModel? SelectedHub
    {
        get => _selectedHub;
        set
        {
            if (_selectedHub == value) return;
            _selectedHub = value;
            NotifyPropertyChanged();
            if (value is null) return;
            EnqueueSelect(value.Id);
        }
    }

    private void EnqueueSelect(string id)
    {
        var previous = _selectHubTask;
        var task = SelectHubChainedAsync(previous, id);
        _selectHubTask = task;
        task.SafeFireAndForget();
    }

    private async Task SelectHubChainedAsync(Task? previous, string id)
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

        await _hubConfigurationProvider.SelectHub(id);
    }

    // Localized string properties
    public string DataSectionTitle => _localizationService.GetString("Settings_Data_SectionTitle");
    public string ClearCacheButton => _localizationService.GetString("Settings_Data_ClearCache");

    public string HubSectionTitle => _localizationService.GetString("Settings_Hub_SectionTitle");
    public string HubSelectLabel => _localizationService.GetString("Settings_Hub_Select");
    public string HubAddHubLabel => _localizationService.GetString("Settings_Hub_AddHub");

    public string ProvidersSectionTitle => _localizationService.GetString("Settings_Data_Providers_SectionTitle");
    public string AddProviderLabel => _localizationService.GetString("Settings_Data_Providers_Add");
    public string ProviderTypeLabel => _localizationService.GetString("Settings_Data_Providers_Type");
    public string AssetTypeLabel => _localizationService.GetString("Settings_Data_Providers_AssetType");
    public string ProviderUrlOrPathLabel => _localizationService.GetString("Settings_Data_Providers_UrlOrPath");
    public string ReloadProvidersLabel => _localizationService.GetString("Settings_Data_Providers_Reload");

    public AssetLoadingViewModel AssetLoading => _assetLoadingViewModel;

    public ObservableCollection<AssetProviderEntryViewModel> AssetProviders { get; } = [];

    public IReadOnlyList<ProviderType> ProviderTypes { get; } =
        [ProviderType.Bucket, ProviderType.GitHub, ProviderType.Filesystem];

    public IReadOnlyList<AssetType> AssetTypes { get; } = [AssetType.Units, AssetType.Hexes];

    public override void AttachHandlers()
    {
        base.AttachHandlers();
        LoadHubsAsync().SafeFireAndForget();
        LoadAssetProvidersAsync().SafeFireAndForget();
    }

    private async Task AddHubAsync()
    {
        var dialog = new AddHubViewModel(_localizationService);
        dialog.SetNavigationService(NavigationService);
        var result = await NavigationService.ShowViewModelForResultAsync<AddHubViewModel, AddHubResult?>(dialog);

        if (result is null) return;

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
            _logger.LogError(ex, "Failed to add hub {HubName}", hub.Name);
        }

        await LoadHubsAsync();
    }

    private async Task RemoveHubAsync(HubEntryViewModel? entry)
    {
        if (entry is null || entry.IsBuiltIn) return;

        await _hubConfigurationProvider.RemoveHub(entry.Id);
        await LoadHubsAsync();
    }

    private async Task OnHubSaved(HubEntryViewModel entry)
    {
        var pending = entry.PendingHub;
        await _hubConfigurationProvider.UpdateHub(entry.Id, pending.Name, pending.BaseUrl, pending.ApiKey);

        await LoadHubsAsync();
    }

    private async Task LoadHubsAsync()
    {
        var hubs = await _hubConfigurationProvider.GetHubs();
        var activeHubId = await _hubConfigurationProvider.GetActiveHubId();

        Hubs.Clear();
        foreach (var hub in hubs)
        {
            Hubs.Add(new HubEntryViewModel(
                hub,
                isNew: false,
                onSaved: OnHubSaved,
                checkStatus: CheckHubStatusAsync));
        }

        _selectedHub = Hubs.FirstOrDefault(h => h.Id == activeHubId);
        NotifyPropertyChanged(nameof(SelectedHub));

        // Fire-and-forget probes so the list renders immediately; badges settle in place.
        foreach (var hub in Hubs)
        {
            hub.RefreshStatusAsync().SafeFireAndForget(
                ex => _logger.LogError(ex, "Error refreshing hub status"));
        }
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
            _logger.LogError(ex, "Hub status probe failed for hub {HubName}", entry.Name);
            return HubStatus.Offline;
        }
    }

    private async Task ClearCacheAsync()
    {
        IsBusy = true;
        try
        {
            CacheStatus = _localizationService.GetString("Settings_Data_Clearing");

            // Clear all caches
            await _fileCachingService.ClearCache();
            await _unitCachingService.ClearCache();
            await _terrainAssetService.ClearCache();

            foreach (var provider in AssetProviders)
                provider.CachedCount = 0;

            CacheStatus = _localizationService.GetString("Settings_Data_Cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            CacheStatus = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> LoadAssetProvidersAsync()
    {
        try
        {
            var providers = await _assetProviderConfigurationProvider.GetProviders();
            var activeCounts = providers
                .Where(p => p.IsActive)
                .GroupBy(p => p.AssetType)
                .ToDictionary(g => g.Key, g => g.Count());
            var cachedCountFormat = _localizationService.GetString("Settings_Data_Providers_Count");

            AssetProviders.Clear();
            foreach (var provider in providers)
            {
                var canDeactivate = provider.IsActive
                    ? activeCounts.GetValueOrDefault(provider.AssetType, 0) > 1
                    : true;
                var entry = new AssetProviderEntryViewModel(
                    provider,
                    onToggleActive: OnAssetProviderToggleActive,
                    onRemove: OnAssetProviderRemove,
                    onSaved: OnAssetProviderSaved,
                    cachedCountFormat: cachedCountFormat)
                {
                    CanDeactivate = canDeactivate
                };
                entry.CachedCount = await GetProviderCachedCountAsync(provider);
                AssetProviders.Add(entry);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load asset providers");
            return false;
        }
    }

    private async Task<int> GetProviderCachedCountAsync(AssetProviderConfigData provider)
    {
        return provider.AssetType switch
        {
            AssetType.Units => await _unitCachingService.GetCachedCount(provider.Id),
            AssetType.Hexes => await _terrainAssetService.GetCachedCount(provider.Id),
            _ => 0
        };
    }

    private async Task OnAssetProviderToggleActive(AssetProviderEntryViewModel entry)
    {
        try
        {
            await _assetProviderConfigurationProvider.SetProviderActive(entry.Id, !entry.IsActive);
            await LoadAssetProvidersAsync();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot toggle provider {ProviderId}", entry.Id);
            // Re-sync the list so row visuals match the persisted active state.
            await LoadAssetProvidersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle provider {ProviderId}", entry.Id);
        }
    }

    private async Task OnAssetProviderRemove(AssetProviderEntryViewModel entry)
    {
        try
        {
            await _assetProviderConfigurationProvider.RemoveProvider(entry.Id);
            await LoadAssetProvidersAsync();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot remove provider {ProviderId}", entry.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove provider {ProviderId}", entry.Id);
        }
    }

    private async Task RemoveAssetProviderAsync(AssetProviderEntryViewModel? entry)
    {
        if (entry is null || entry.CanRemove is false) return;

        await OnAssetProviderRemove(entry);
    }

    private async Task OnAssetProviderSaved(AssetProviderEntryViewModel entry)
    {
        try
        {
            await _assetProviderConfigurationProvider.UpdateProvider(entry.Id, entry.PendingProvider);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot update provider {ProviderId}", entry.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update provider {ProviderId}", entry.Id);
            throw;
        }

        // The write succeeded but the reload failed: report the save as failed so the editor
        // stays open with the pending values instead of closing on a stale list.
        if (!await LoadAssetProvidersAsync())
        {
            throw new InvalidOperationException("Failed to reload asset providers after save");
        }
    }

    private async Task AddProviderAsync()
    {
        var dialog = new AddProviderViewModel(ProviderTypes, AssetTypes, _localizationService);
        dialog.SetNavigationService(NavigationService);
        var result = await NavigationService.ShowViewModelForResultAsync<AddProviderViewModel, AddProviderResult?>(dialog);

        if (result is null) return;

        try
        {
            var providers = await _assetProviderConfigurationProvider.GetProviders();
            var nextSortOrder = providers.Count == 0 ? 0 : providers.Max(p => p.SortOrder) + 1;
            var provider = new AssetProviderConfigData(
                Guid.NewGuid().ToString("N"),
                result.ProviderType,
                result.AssetType,
                result.UrlOrPath,
                IsActive: true,
                IsDefault: false,
                nextSortOrder);

            await _assetProviderConfigurationProvider.AddProvider(provider);
            await LoadAssetProvidersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add asset provider");
        }
    }

    private async Task ReloadProvidersAsync()
    {
        IsBusy = true;
        try
        {
            // Reload caches from the persisted (possibly externally changed) provider set first,
            // then re-read the list so row visuals and cached counts reflect the new set.
            await _assetLoadingViewModel.ReloadAsync();
            await LoadAssetProvidersAsync();
            CacheStatus = _localizationService.GetString("Settings_Data_Reloaded");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload asset providers");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
