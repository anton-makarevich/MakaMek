using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Services.ResourceProviders;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// Provides pre-existing maps from embedded assembly resources
/// </summary>
public class EmbeddedMapResourceProvider : IMapResourceProvider
{
    private readonly ILogger<EmbeddedMapResourceProvider> _logger;
    private const string MapsResourcePrefix = "Resources.Maps";
    private readonly AssemblyResourceStreamProvider _streamProvider;

    public EmbeddedMapResourceProvider(ILogger<EmbeddedMapResourceProvider> logger)
    {
        _logger = logger;
        _streamProvider = new AssemblyResourceStreamProvider("json", typeof(EmbeddedMapResourceProvider).Assembly);
    }

    public async Task<IReadOnlyList<(string Name, IList<HexData> HexData)>> GetAvailableMapsAsync()
    {
        var resourceIds = await _streamProvider.GetAvailableResourceIds();
        var mapResourceIds = resourceIds
            .Where(id => id.Contains(MapsResourcePrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var maps = new List<(string Name, IList<HexData> HexData)>();

        foreach (var resourceId in mapResourceIds)
        {
            try
            {
                await using var stream = await _streamProvider.GetResourceStream(resourceId);
                if (stream == null) continue;

                var hexData = await JsonSerializer.DeserializeAsync<List<HexData>>(stream);
                if (hexData == null) continue;

                var name = ExtractMapName(resourceId);
                maps.Add((name, hexData));
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
