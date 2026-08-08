using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Services.Transport.Relay;
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
    private readonly ILogger<SettingsViewModel> _logger;
    private HubEntryViewModel? _selectedHub;

    public SettingsViewModel(
        IFileCachingService fileCachingService,
        IUnitCachingService unitCachingService,
        ITerrainAssetService terrainAssetService,
        ILocalizationService localizationService,
        IRelayHubConfigurationProvider hubConfigurationProvider,
        ILogger<SettingsViewModel> logger)
    {
        _fileCachingService = fileCachingService;
        _unitCachingService = unitCachingService;
        _terrainAssetService = terrainAssetService;
        _localizationService = localizationService;
        _hubConfigurationProvider = hubConfigurationProvider;
        _logger = logger;

        ClearCacheCommand = new AsyncCommand(ClearCacheAsync);
        AddHubCommand = new AsyncCommand(AddHubAsync);
        RemoveHubCommand = new AsyncCommand<HubEntryViewModel>(RemoveHubAsync);

        // Initialize cache status
        InitializeCacheStatusAsync().SafeFireAndForget();
    }

    public ICommand ClearCacheCommand { get; }
    public ICommand AddHubCommand { get; }
    public ICommand RemoveHubCommand { get; }

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
            NotifyPropertyChanged(nameof(SelectedHub));
            if (value is null) return;
            _hubConfigurationProvider.SelectHubAsync(value.Id).SafeFireAndForget();
        }
    }

    // Localized string properties
    public string DataSectionTitle => _localizationService.GetString("Settings_Data_SectionTitle");
    public string ClearCacheButton => _localizationService.GetString("Settings_Data_ClearCache");
    public string ClearCacheDescription => _localizationService.GetString("Settings_Data_ClearCacheDescription");

    public string HubSectionTitle => _localizationService.GetString("Settings_Hub_SectionTitle");
    public string HubSelectLabel => _localizationService.GetString("Settings_Hub_Select");
    public string HubAddHubLabel => _localizationService.GetString("Settings_Hub_AddHub");

    public override void AttachHandlers()
    {
        base.AttachHandlers();
        LoadHubsAsync().SafeFireAndForget();
    }

    private async Task AddHubAsync()
    {
        var entry = new HubEntryViewModel(
            new HubConfigData(Guid.NewGuid().ToString("N"), string.Empty, string.Empty, string.Empty, false),
            isNew: true,
            onSaved: OnHubSaved,
            onCancelled: OnHubEditCancelled);

        Hubs.Add(entry);
        _selectedHub = entry;
        NotifyPropertyChanged(nameof(SelectedHub));
        await entry.StartEditing();
    }

    private async Task RemoveHubAsync(HubEntryViewModel entry)
    {
        if (entry.IsBuiltIn) return;

        await _hubConfigurationProvider.RemoveHubAsync(entry.Id);
        await LoadHubsAsync();
    }

    private async Task OnHubSaved(HubEntryViewModel entry)
    {
        if (entry.IsNew)
        {
            await _hubConfigurationProvider.AddHubAsync(entry.Hub);
        }
        else
        {
            await _hubConfigurationProvider.UpdateHubAsync(entry.Id, entry.Name, entry.BaseUrl, entry.ApiKey);
        }

        await LoadHubsAsync();
    }

    private void OnHubEditCancelled(HubEntryViewModel entry)
    {
        if (entry.IsNew)
        {
            Hubs.Remove(entry);
        }
    }

    private async Task LoadHubsAsync()
    {
        await _hubConfigurationProvider.EnsureLoadedAsync();

        Hubs.Clear();
        foreach (var hub in _hubConfigurationProvider.Hubs)
        {
            Hubs.Add(new HubEntryViewModel(
                hub,
                isNew: false,
                onSaved: OnHubSaved,
                onCancelled: OnHubEditCancelled));
        }

        _selectedHub = Hubs.FirstOrDefault(h => h.Id == _hubConfigurationProvider.ActiveHubId);
        NotifyPropertyChanged(nameof(SelectedHub));
    }

    private async Task InitializeCacheStatusAsync()
    {
        try
        {
            var models = await _unitCachingService.GetAvailableModels();
            var biomes = await _terrainAssetService.GetLoadedBiomes();
            var unitCount = models.Count();
            var biomeCount = biomes.Count();

            CacheStatus = string.Format(
                _localizationService.GetString("Settings_Data_CacheStatus"),
                unitCount,
                biomeCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize cache status");
            CacheStatus = _localizationService.GetString("Settings_Data_CacheStatus");
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
            _unitCachingService.ClearCache();
            _terrainAssetService.ClearCache();

            CacheStatus = _localizationService.GetString("Settings_Data_Cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            CacheStatus = _localizationService.GetString("Settings_Data_CacheStatus");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
