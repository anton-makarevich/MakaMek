using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Services;
using Sanet.MakaMek.Services.ResourceProviders;

namespace Sanet.MakaMek.Services.Avalonia;

/// <summary>
/// Provides pre-existing maps from embedded assembly resources
/// </summary>
public class EmbeddedMapResourceProvider : IMapResourceProvider
{
    private readonly ILogger<EmbeddedMapResourceProvider> _logger;
    private const string MapsResourcePrefix = "Resources.Maps";
    private readonly IResourceStreamProvider _streamProvider;

    public EmbeddedMapResourceProvider(ILogger<EmbeddedMapResourceProvider> logger, IResourceStreamProvider streamProvider)
    {
        _logger = logger;
        _streamProvider = streamProvider;
    }

    public async Task<IReadOnlyList<(string Name, BattleMapData MapData)>> GetAvailableMapsAsync()
    {
        var resourceIds = await _streamProvider.GetAvailableResourceIds();
        var mapResourceIds = resourceIds
            .Where(id => id.Contains(MapsResourcePrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var maps = new List<(string Name, BattleMapData MapData)>();

        foreach (var resourceId in mapResourceIds)
        {
            try
            {
                await using var stream = await _streamProvider.GetResourceStream(resourceId);
                if (stream == null) continue;

                var mapData = await JsonSerializer.DeserializeAsync<BattleMapData>(stream);
                if (mapData?.HexData == null || mapData.HexData.Count == 0)
                {
                    _logger.LogWarning("Skipping map resource {ResourceId} because it has no hex data", resourceId);
                    continue;
                }

                var name = ExtractMapName(resourceId);
                maps.Add((name, mapData));
            }
            catch (JsonException exception)
            {
                _logger.LogError(exception, "Error deserializing map data from {ResourceId}", resourceId);
            }
        }

        return maps;
    }

    /// <summary>
    /// Extracts a human-readable map name from the fully qualified resource name.
    /// </summary>
    private static string ExtractMapName(string resourceId)
    {
        // Remove the .json extension
        var withoutExtension = resourceId.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? resourceId[..^5]
            : resourceId;

        // Get the last segment after the last dot (the filename without extension)
        var lastDotIndex = withoutExtension.LastIndexOf('.');
        return lastDotIndex >= 0
            ? withoutExtension[(lastDotIndex + 1)..]
            : withoutExtension;
    }
}
