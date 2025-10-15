using System.Reactive.Linq;
using System.Reactive.Subjects;
using AsyncAwaitBestPractices;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Core.Utils.Generators;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// ViewModel for map configuration settings
/// </summary>
public class MapConfigViewModel : BindableBase, IDisposable
{
    private int _mapWidth = 15;
    private int _mapHeight = 17;
    private int _forestCoverage = 20;
    private int _lightWoodsPercentage = 30;
    private object? _previewImage;
    private bool _isGenerating;
    private readonly IMapPreviewRenderer _previewRenderer;
    private readonly IBattleMapFactory _mapFactory;
    private readonly IDisposable? _previewSubscription;
    private readonly Subject<MapParameterChange> _mapParametersChanged = new();

    public MapConfigViewModel(IMapPreviewRenderer previewRenderer, IBattleMapFactory mapFactory)
    {
        _previewRenderer = previewRenderer;
        _mapFactory = mapFactory;

        // Subscribe with debounce
        _previewSubscription = _mapParametersChanged
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Subscribe( (mapParameterChange) =>
            {
                UpdateMapAsync().SafeFireAndForget();
            });

        // Generate initial map and preview
        UpdateMapAsync().SafeFireAndForget();

    }

    public string MapWidthLabel => "Map Width";
    public string MapHeightLabel => "Map Height";
    public string ForestCoverageLabel => "Forest Coverage";
    public string LightWoodsLabel => "Light Woods Percentage";

    public int MapWidth
    {
        get => _mapWidth;
        set
        {
            SetProperty(ref _mapWidth, value);
            StartMapUpdate();
        }
    }

    private void StartMapUpdate()
    {
        IsGenerating = true;
        _mapParametersChanged.OnNext(MapParameterChange.Instance);
    }

    public int MapHeight
    {
        get => _mapHeight;
        set
        {
            SetProperty(ref _mapHeight, value);
            StartMapUpdate();
        }
    }

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
        get => _lightWoodsPercentage;
        set
        {
            SetProperty(ref _lightWoodsPercentage, value);
            StartMapUpdate();
        }
    }

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
        get => _isGenerating;
        private set => SetProperty(ref _isGenerating, value);
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

