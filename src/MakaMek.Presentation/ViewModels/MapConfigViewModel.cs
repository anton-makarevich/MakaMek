using System.Reactive.Linq;
using System.Reactive.Subjects;
using AsyncAwaitBestPractices;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// ViewModel for map configuration settings
/// </summary>
public class MapConfigViewModel : BindableBase, IDisposable
{
    private int _forestCoverage = 20;
    private object? _previewImage;
    private readonly IMapPreviewRenderer _previewRenderer;
    private readonly IBattleMapFactory _mapFactory;
    private readonly IDisposable? _previewSubscription;
    private readonly Subject<MapParameterChange> _mapParametersChanged = new();

    public MapConfigViewModel(IMapPreviewRenderer previewRenderer, IBattleMapFactory mapFactory, ILogger logger)
    {
        _previewRenderer = previewRenderer;
        _mapFactory = mapFactory;
        var logger1 = logger;

        // Subscribe with debouncing
        _previewSubscription = _mapParametersChanged
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Subscribe( (_) =>
            {
                UpdateMapAsync().SafeFireAndForget(ex => logger1.LogError(ex, "Error updating map"));
            });

        // Generate an initial map and preview
        UpdateMapAsync().SafeFireAndForget(ex => logger1.LogError(ex, "Error generating initial map"));
    }

    public string MapWidthLabel => "Map Width";
    public string MapHeightLabel => "Map Height";
    public string ForestCoverageLabel => "Forest Coverage";
    public string LightWoodsLabel => "Light Woods Percentage";

    public int MapWidth
    {
        get;
        set
        {
            SetProperty(ref field, value);
            StartMapUpdate();
        }
    } = 15;

    private void StartMapUpdate()
    {
        IsGenerating = true;
        _mapParametersChanged.OnNext(MapParameterChange.Instance);
    }

    public int MapHeight
    {
        get;
        set
        {
            SetProperty(ref field, value);
            StartMapUpdate();
        }
    } = 17;

    public int ForestCoverage
    {
        get => _forestCoverage;
        set
        {
            SetProperty(ref _forestCoverage, value);
            NotifyPropertyChanged(nameof(IsLightWoodsEnabled));
            StartMapUpdate();
        }
    }

    public int LightWoodsPercentage
    {
        get;
        set
        {
            SetProperty(ref field, value);
            StartMapUpdate();
        }
    } = 30;

    public bool IsLightWoodsEnabled => _forestCoverage > 0;

    // Expose the actual map that will be used
    public BattleMap? Map { get; private set; }

    public object? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    public bool IsGenerating
    {
        get;
        private set => SetProperty(ref field, value);
    }

    // Renamed and repurposed: now generates the actual BattleMap and optionally updates the preview
    private async Task UpdateMapAsync()
    {
        try
        {
            ITerrainGenerator generator = ForestCoverage == 0
                ? new SingleTerrainGenerator(MapWidth, MapHeight, new ClearTerrain())
                : new ForestPatchesGenerator(
                    MapWidth,
                    MapHeight,
                    forestCoverage: ForestCoverage / 100.0,
                    lightWoodsProbability: LightWoodsPercentage / 100.0);

            Map = _mapFactory.GenerateMap(MapWidth, MapHeight, generator);

            // Update preview image based on the generated map
            var oldPreview = _previewImage as IDisposable;
            if (Map != null)
            {
                PreviewImage = await _previewRenderer.GeneratePreviewAsync(Map);
                oldPreview?.Dispose();
            }
        }
        finally
        {
            IsGenerating = false;
        }
    }

    public void Dispose()
    {
        (_previewImage as IDisposable)?.Dispose();
        _previewSubscription?.Dispose();
        _mapParametersChanged.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Marker type for map parameter change events
/// </summary>
internal readonly struct MapParameterChange
{
    public static readonly MapParameterChange Instance = new();
}

