using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Data.Game.Players;
using Sanet.MakaMek.Presentation.Models.Logger;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Presentation.ViewModels.Setup;

/// <summary>
/// Loads and persists the default player used to prefill the lobby, including the fresh-player tint
/// generated when the cache is empty or unreadable. Kept out of the setup session so it does not
/// depend on the cache format.
/// </summary>
public sealed class DefaultPlayerStore
{
    private const string DefaultPlayerCacheKey = "DefaultPlayer";

    private readonly IFileCachingService _cachingService;

    public DefaultPlayerStore(IFileCachingService cachingService)
    {
        _cachingService = cachingService;
    }

    /// <summary>
    /// Loads the cached default player, creating and persisting a fresh one when the cache is empty
    /// or unreadable.
    /// </summary>
    public async Task<PlayerData> LoadOrCreateAsync(ILogger? gameLogger)
    {
        try
        {
            var cachedData = await _cachingService.TryGetCachedFile(DefaultPlayerCacheKey);
            if (cachedData != null)
            {
                var json = System.Text.Encoding.UTF8.GetString(cachedData);
                return JsonSerializer.Deserialize<PlayerData>(json);
            }
        }
        catch (Exception ex)
        {
            gameLogger?.LogErrorLoadingDefaultPlayerFromCache(ex);
        }

        var defaultPlayerData = PlayerData.CreateDefault() with { Tint = PlayerTint.Generate() };
        await SaveAsync(defaultPlayerData, gameLogger);
        return defaultPlayerData;
    }

    /// <summary>
    /// Persists the default player so the next launch reuses its name and tint.
    /// </summary>
    public async Task SaveAsync(PlayerData playerData, ILogger? gameLogger)
    {
        try
        {
            var json = JsonSerializer.Serialize(playerData);
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            await _cachingService.SaveToCache(DefaultPlayerCacheKey, bytes);
        }
        catch (Exception ex)
        {
            gameLogger?.LogErrorSavingDefaultPlayerToCache(ex);
        }
    }
}
