using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Services;
using Sanet.MakaMek.Presentation.Models.Logger;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Presentation.ViewModels.Maps;

/// <summary>
/// Reads the pre-existing maps, builds the catalogue rows, renders their previews, and loads/exports
/// map files. Kept out of <see cref="MapConfigViewModel"/> so the view model only sees
/// <see cref="MapPreviewItem"/> rows and never the map data, catalogue or file format.
/// </summary>
public sealed class AvailableMapsService
{
    private readonly IMapResourceProvider _mapResourceProvider;
    private readonly IBattleMapFactory _mapFactory;
    private readonly IMapPreviewRenderer _previewRenderer;
    private readonly IFileService _fileService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger<AvailableMapsService> _logger;

    public AvailableMapsService(
        IMapResourceProvider mapResourceProvider,
        IBattleMapFactory mapFactory,
        IMapPreviewRenderer previewRenderer,
        IFileService fileService,
        IDispatcherService dispatcherService,
        ILogger<AvailableMapsService> logger)
    {
        _mapResourceProvider = mapResourceProvider;
        _mapFactory = mapFactory;
        _previewRenderer = previewRenderer;
        _fileService = fileService;
        _dispatcherService = dispatcherService;
        _logger = logger;
    }

    /// <summary>
    /// Reads the available maps and materialises one row per map. Previews are rendered later through
    /// <see cref="RenderPreviewAsync"/> so the rows appear before any rendering finishes.
    /// </summary>
    public async Task<IReadOnlyList<MapPreviewItem>> GetAvailableMapsAsync()
    {
        var maps = await _mapResourceProvider.GetAvailableMapsAsync();
        var items = new List<MapPreviewItem>(maps.Count);

        foreach (var (name, mapData) in maps)
        {
            items.Add(new MapPreviewItem
            {
                Name = name,
                Map = _mapFactory.CreateFromData(mapData),
                PreviewImage = null
            });
        }

        return items;
    }

    /// <summary>
    /// Renders <paramref name="item"/>'s preview and applies it on the UI thread. Failures are logged
    /// and leave the preview empty.
    /// </summary>
    public async Task RenderPreviewAsync(MapPreviewItem item)
    {
        try
        {
            var preview = await _previewRenderer.GeneratePreview(item.Map!);
            _dispatcherService.RunOnUIThread(() => item.PreviewImage = preview);
        }
        catch (Exception ex)
        {
            _logger.LogErrorGeneratingPreviewForMap(item.Name, ex);
            // Leave PreviewImage as null on failure
        }
    }

    /// <summary>
    /// Shows the open-file dialog, parses the selected map and renders its preview. Returns
    /// <c>null</c> when the dialog was cancelled or the file holds no map.
    /// </summary>
    public async Task<MapPreviewItem?> LoadFromFileAsync(string dialogTitle)
    {
        var (name, content) = await _fileService.OpenFile(dialogTitle);
        if (string.IsNullOrWhiteSpace(content)) return null;

        return await CreateItemFromFileAsync(name, content);
    }

    /// <summary>
    /// Parses a map file into a selectable row with a rendered preview. Returns <c>null</c> when the
    /// content is not a map or has no hexes.
    /// </summary>
    public async Task<MapPreviewItem?> CreateItemFromFileAsync(string? name, string content)
    {
        var mapData = JsonSerializer.Deserialize<BattleMapData>(content);
        if (mapData?.HexData is not { Count: > 0 }) return null;

        var battleMap = _mapFactory.CreateFromData(mapData);
        var mapName = string.IsNullOrWhiteSpace(name)
            ? "Loaded Map"
            : Path.GetFileNameWithoutExtension(name);

        return new MapPreviewItem
        {
            Name = mapName,
            Map = battleMap,
            PreviewImage = await _previewRenderer.GeneratePreview(battleMap)
        };
    }

    /// <summary>
    /// Serialises <paramref name="map"/> and saves it through the file service.
    /// </summary>
    public async Task ExportAsync(BattleMap map, string dialogTitle)
    {
        try
        {
            var data = map.ToData();
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            await _fileService.SaveJsonFile(dialogTitle, "map.json", json);
        }
        catch (Exception ex)
        {
            _logger.LogFailedToExportMap(ex);
        }
    }
}
