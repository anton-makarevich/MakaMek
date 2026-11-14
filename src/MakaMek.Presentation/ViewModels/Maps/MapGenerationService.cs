using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Services;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Presentation.ViewModels.Maps;

/// <summary>
/// The map-generation parameters captured from the map config screen in one value, so the generator
/// service does not need eleven positional arguments.
/// </summary>
public readonly record struct MapGenerationSettings(
    int Width,
    int Height,
    int ForestCoverage,
    int LightWoodsPercentage,
    int RoughCoverage,
    int HillCoverage,
    int MaxElevation,
    int LakeCoverage,
    int LakeMaxDepth,
    int RiverCount,
    int RoadCount);

/// <summary>
/// Builds procedurally generated maps and renders their previews. Kept out of
/// <see cref="MapConfigViewModel"/> so the view model does not depend on the generator builder or the
/// terrain types.
/// </summary>
public sealed class MapGenerationService
{
    private readonly IBattleMapFactory _mapFactory;
    private readonly IMapPreviewRenderer _previewRenderer;

    public MapGenerationService(IBattleMapFactory mapFactory, IMapPreviewRenderer previewRenderer)
    {
        _mapFactory = mapFactory;
        _previewRenderer = previewRenderer;
    }

    /// <summary>
    /// Generates a map for the supplied parameters, applying every terrain layer that is enabled
    /// (coverage above zero).
    /// </summary>
    public BattleMap? Generate(MapGenerationSettings settings)
    {
        var builder = new MapGeneratorBuilder(settings.Width, settings.Height)
            .WithBaseTerrain(new ClearTerrain());

        if (settings.ForestCoverage > 0)
            builder = builder.WithForestPatches(settings.ForestCoverage / 100.0, settings.LightWoodsPercentage / 100.0);

        if (settings.RoughCoverage > 0)
            builder = builder.WithTerrain<RoughTerrain>(settings.RoughCoverage / 100.0);

        if (settings.HillCoverage > 0)
            builder = builder.WithHills(settings.HillCoverage / 100.0, settings.MaxElevation);

        if (settings.LakeCoverage > 0)
            builder = builder.WithLakes(settings.LakeCoverage / 100.0, settings.LakeMaxDepth);

        if (settings.RiverCount > 0)
            builder = builder.WithRivers(settings.RiverCount);

        if (settings.RoadCount > 0)
            builder = builder.WithRoads(settings.RoadCount);

        return _mapFactory.GenerateMap(settings.Width, settings.Height, builder.Build());
    }

    /// <summary>
    /// Renders a preview of <paramref name="map"/> on the current thread; cancellation is the caller's
    /// concern.
    /// </summary>
    public Task<object?> RenderPreviewAsync(BattleMap map, CancellationToken cancellationToken = default) =>
        _previewRenderer.GeneratePreview(map, cancellationToken: cancellationToken);
}
