using Microsoft.Extensions.Logging;
using Sanet.Localization;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Presentation.ViewModels.Wrappers;
using Sanet.MVVM.Core.Services;

namespace Sanet.MakaMek.Presentation.ViewModels.Settings;

/// <summary>
/// Owns the asset-provider section of the settings screen: reading the configured providers, their
/// cached counts, the activation/removal/edit writes and the add-provider dialog. The view model only
/// binds the returned row view models.
/// </summary>
public sealed class AssetProviderSettingsService
{
    private readonly IAssetProviderConfigurationProvider _assetProviderConfigurationProvider;
    private readonly IUnitCachingService _unitCachingService;
    private readonly ITerrainAssetService _terrainAssetService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogger<AssetProviderSettingsService> _logger;

    public AssetProviderSettingsService(
        IAssetProviderConfigurationProvider assetProviderConfigurationProvider,
        IUnitCachingService unitCachingService,
        ITerrainAssetService terrainAssetService,
        ILocalizationService localizationService,
        ILogger<AssetProviderSettingsService> logger)
    {
        _assetProviderConfigurationProvider = assetProviderConfigurationProvider;
        _unitCachingService = unitCachingService;
        _terrainAssetService = terrainAssetService;
        _localizationService = localizationService;
        _logger = logger;
    }

    /// <summary>
    /// Reads the persisted providers and builds one row per provider, including its cached count and
    /// whether it may be deactivated. Returns <c>failures</c> when the catalogue could not be read.
    /// </summary>
    public async Task<AssetProviderSnapshot> LoadAsync(
        Func<AssetProviderEntryViewModel, Task> onToggleActive,
        Func<AssetProviderEntryViewModel, Task> onRemove,
        Func<AssetProviderEntryViewModel, Task> onSaved)
    {
        try
        {
            var cachedCountFormat = _localizationService.GetString("Settings_Data_Providers_Count");
            var providers = await _assetProviderConfigurationProvider.GetProviders();
            var activeCounts = providers
                .Where(p => p.IsActive)
                .GroupBy(p => p.AssetType)
                .ToDictionary(g => g.Key, g => g.Count());

            var entries = new List<AssetProviderEntryViewModel>(providers.Count);
            foreach (var provider in providers)
            {
                var canDeactivate = provider.IsActive
                    ? activeCounts.GetValueOrDefault(provider.AssetType, 0) > 1
                    : true;
                var entry = new AssetProviderEntryViewModel(
                    provider,
                    onToggleActive: onToggleActive,
                    onRemove: onRemove,
                    onSaved: onSaved,
                    cachedCountFormat: cachedCountFormat)
                {
                    CanDeactivate = canDeactivate
                };
                entry.CachedCount = await GetCachedCountAsync(provider);
                entries.Add(entry);
            }

            return new AssetProviderSnapshot(entries, Succeeded: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load asset providers");
            return new AssetProviderSnapshot([], Succeeded: false);
        }
    }

    /// <summary>
    /// Flips the persisted activation of <paramref name="entry"/>. Returns whether the list must be
    /// reloaded so the rows match the persisted state.
    /// </summary>
    public async Task<bool> ToggleActiveAsync(AssetProviderEntryViewModel entry)
    {
        try
        {
            await _assetProviderConfigurationProvider.SetProviderActive(entry.Id, !entry.IsActive);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot toggle provider {ProviderId}", entry.Id);
            // Re-sync the list so row visuals match the persisted active state.
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle provider {ProviderId}", entry.Id);
            return false;
        }
    }

    /// <summary>
    /// Removes <paramref name="entry"/>. Returns whether the list must be reloaded.
    /// </summary>
    public async Task<bool> RemoveAsync(AssetProviderEntryViewModel entry)
    {
        if (entry.CanRemove is false) return false;

        try
        {
            await _assetProviderConfigurationProvider.RemoveProvider(entry.Id);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot remove provider {ProviderId}", entry.Id);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove provider {ProviderId}", entry.Id);
            return false;
        }
    }

    /// <summary>
    /// Persists the current <paramref name="entry"/> edits, rethrowing on failure so the editor stays
    /// open with the pending values.
    /// </summary>
    public async Task SaveAsync(AssetProviderEntryViewModel entry)
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
    }

    /// <summary>
    /// Shows the add-provider dialog and persists the result. Returns <c>true</c> when the caller
    /// should reload the list.
    /// </summary>
    public async Task<bool> AddAsync(INavigationService navigationService)
    {
        var dialog = new AddProviderViewModel(
            [ProviderType.Bucket, ProviderType.GitHub, ProviderType.Filesystem],
            [AssetType.Units, AssetType.Hexes],
            _localizationService);
        dialog.SetNavigationService(navigationService);
        var result = await navigationService.ShowViewModelForResultAsync<AddProviderViewModel, AddProviderResult?>(dialog);

        if (result is null) return false;

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
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add asset provider");
            return false;
        }
    }

    private async Task<int> GetCachedCountAsync(AssetProviderConfigData provider)
    {
        return provider.AssetType switch
        {
            AssetType.Units => await _unitCachingService.GetCachedCount(provider.Id),
            AssetType.Hexes => await _terrainAssetService.GetCachedCount(provider.Id),
            _ => 0
        };
    }
}

/// <summary>
/// The provider rows read in one pass, plus whether the catalogue could be read at all.
/// </summary>
public sealed record AssetProviderSnapshot(IReadOnlyList<AssetProviderEntryViewModel> Providers, bool Succeeded);
