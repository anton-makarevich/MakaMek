using System.Collections.ObjectModel;
using System.Windows.Input;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.Localization;
using Sanet.MakaMek.Presentation.ViewModels.Settings;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly ILocalizationService _localizationService;
    private readonly HubSettingsService _hubSettings;
    private readonly AssetProviderSettingsService _assetProviderSettings;
    private readonly AssetCacheService _assetCache;
    private readonly AssetLoadingViewModel _assetLoadingViewModel;
    private readonly ILogger<SettingsViewModel> _logger;
    private HubEntryViewModel? _selectedHub;

    /// <summary>
    /// Creates the settings screen. The section coordinators are resolved by the composition root's
    /// settings-services registration; this screen only binds their results and forwards the row
    /// commands.
    /// </summary>
    public SettingsViewModel(
        ILocalizationService localizationService,
        AssetLoadingViewModel assetLoadingViewModel,
        HubSettingsService hubSettings,
        AssetProviderSettingsService assetProviderSettings,
        AssetCacheService assetCache,
        ILogger<SettingsViewModel> logger)
    {
        _localizationService = localizationService;
        _assetLoadingViewModel = assetLoadingViewModel;
        _logger = logger;
        _hubSettings = hubSettings;
        _assetProviderSettings = assetProviderSettings;
        _assetCache = assetCache;

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

    public string DataStatus
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
            _hubSettings.Select(value.Id);
        }
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
        if (await _hubSettings.AddAsync(NavigationService))
        {
            await LoadHubsAsync();
        }
    }

    private async Task RemoveHubAsync(HubEntryViewModel? entry)
    {
        if (entry is null || entry.IsBuiltIn) return;

        await _hubSettings.RemoveAsync(entry);
        await LoadHubsAsync();
    }

    private async Task OnHubSaved(HubEntryViewModel entry)
    {
        await _hubSettings.SaveAsync(entry);
        await LoadHubsAsync();
    }

    private async Task LoadHubsAsync()
    {
        var snapshot = await _hubSettings.LoadAsync(OnHubSaved);

        Hubs.Clear();
        foreach (var hub in snapshot.Hubs)
        {
            Hubs.Add(hub);
        }

        _selectedHub = Hubs.FirstOrDefault(h => h.Id == snapshot.ActiveHubId);
        NotifyPropertyChanged(nameof(SelectedHub));
    }

    private async Task ClearCacheAsync()
    {
        IsBusy = true;
        try
        {
            DataStatus = _localizationService.GetString("Settings_Data_Clearing");

            await _assetCache.ClearAllAsync();

            foreach (var provider in AssetProviders)
                provider.CachedCount = 0;

            DataStatus = _localizationService.GetString("Settings_Data_Cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cache");
            DataStatus = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> LoadAssetProvidersAsync()
    {
        var snapshot = await _assetProviderSettings.LoadAsync(
            OnAssetProviderToggleActive,
            OnAssetProviderRemove,
            OnAssetProviderSaved);

        // Keep the rows the user can see when the catalogue read fails: a transient provider
        // error must not empty the section.
        if (!snapshot.Succeeded) return false;

        AssetProviders.Clear();
        foreach (var provider in snapshot.Providers)
        {
            AssetProviders.Add(provider);
        }

        return true;
    }

    private async Task OnAssetProviderToggleActive(AssetProviderEntryViewModel entry)
    {
        if (await _assetProviderSettings.ToggleActiveAsync(entry))
        {
            await LoadAssetProvidersAsync();
        }
    }

    private async Task OnAssetProviderRemove(AssetProviderEntryViewModel entry)
    {
        if (await _assetProviderSettings.RemoveAsync(entry))
        {
            await LoadAssetProvidersAsync();
        }
    }

    private async Task RemoveAssetProviderAsync(AssetProviderEntryViewModel? entry)
    {
        if (entry is null || entry.CanRemove is false) return;

        await OnAssetProviderRemove(entry);
    }

    private async Task OnAssetProviderSaved(AssetProviderEntryViewModel entry)
    {
        await _assetProviderSettings.SaveAsync(entry);

        // The write succeeded but the reload failed: report the save as failed so the editor
        // stays open with the pending values instead of closing on a stale list.
        if (!await LoadAssetProvidersAsync())
        {
            throw new InvalidOperationException("Failed to reload asset providers after save");
        }
    }

    private async Task AddProviderAsync()
    {
        if (await _assetProviderSettings.AddAsync(NavigationService))
        {
            await LoadAssetProvidersAsync();
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
            var reloaded = await LoadAssetProvidersAsync();
            DataStatus = reloaded
                ? _localizationService.GetString("Settings_Data_Reloaded")
                : string.Empty;
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
