using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using System.Text.Json;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Services;
using Sanet.MVVM.Core.ViewModels;

namespace Sanet.MakaMek.Presentation.ViewModels;

/// <summary>
/// ViewModel for map configuration settings with tab support for
/// selecting pre-existing maps or generating new ones
/// </summary>
public class MapConfigViewModel : BindableBase, IDisposable
{
    private int _forestCoverage = 20;
    private object? _previewImage;
    private readonly IMapPreviewRenderer _previewRenderer;
    private readonly IBattleMapFactory _mapFactory;
    private readonly IMapResourceProvider _mapResourceProvider;
    private readonly IFileService _fileService;
    private readonly IDisposable? _previewSubscription;
    private readonly Subject<MapParameterChange> _mapParametersChanged = new();
    private readonly ILogger _logger;

    public MapConfigViewModel(
        IMapPreviewRenderer previewRenderer,
        IBattleMapFactory mapFactory,
        IMapResourceProvider mapResourceProvider,
        IFileService fileService,
        ILogger logger)
    {
        _previewRenderer = previewRenderer;
        _mapFactory = mapFactory;
        _mapResourceProvider = mapResourceProvider;
        _fileService = fileService;
        _logger = logger;
        
        LoadMapCommand = new AsyncCommand(LoadMap);

        // Subscribe with debouncing
        _previewSubscription = _mapParametersChanged
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Subscribe( (_) =>
            {
                UpdateMap().SafeFireAndForget(ex => _logger.LogError(ex, "Error updating map"));
            });

        // Generate an initial map and preview
        UpdateMap().SafeFireAndForget(ex => _logger.LogError(ex, "Error generating initial map"));
        
        // Load available pre-existing maps
        LoadAvailableMaps().SafeFireAndForget(ex => _logger.LogError(ex, "Error loading available maps"));
    }

    public string MapWidthLabel => "Map Width";
    public string MapHeightLabel => "Map Height";
    public string ForestCoverageLabel => "Forest Coverage";
    public string LightWoodsLabel => "Light Woods Percentage";

    /// <summary>
    /// Currently selected tab index. 0 = Select Map, 1 = Generate Map
    /// </summary>
    public int SelectedTabIndex
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(Map));
        }
    }

    /// <summary>
    /// Collection of pre-existing maps available for selection
    /// </summary>
    public ObservableCollection<MapPreviewItem> AvailableMaps { get; } = [];

    /// <summary>
    /// Currently selected pre-existing map
    /// </summary>
    public MapPreviewItem? SelectedMap
    {
        get;
        private set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(Map));
        }
    }

    /// <summary>
    /// Whether maps are currently being loaded
    /// </summary>
    public bool IsLoadingMaps
    {
        get;
        private set => SetProperty(ref field, value);
    }

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

    /// <summary>
    /// The active map depending on which tab is selected.
    /// Tab 0 (Select Map): returns the selected pre-existing map.
    /// Tab 1 (Generate Map): returns the procedurally generated map.
    /// </summary>
    public BattleMap? Map => SelectedTabIndex == 0 ? SelectedMap?.Map : GeneratedMap;

    /// <summary>
    /// The procedurally generated map (from the Generate tab)
    /// </summary>
    private BattleMap? GeneratedMap { get; set; }

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

    /// <summary>
    /// Selects the specified map item and deselects all others
    /// </summary>
    public void SelectMap(MapPreviewItem item)
    {
        foreach (var map in AvailableMaps)
        {
            map.IsSelected = map == item;
        }
        SelectedMap = item;
    }

    public IAsyncCommand LoadMapCommand { get; }

    private async Task LoadMap()
    {
        try
        {
            var (name, content) = await _fileService.OpenFile("Select Map File");
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var hexData = JsonSerializer.Deserialize<List<HexData>>(content);
            if (hexData == null || hexData.Count == 0)
            {
                return;
            }

            var battleMap = _mapFactory.CreateFromData(hexData);

            var mapName = string.IsNullOrWhiteSpace(name)
                ? "Loaded Map"
                : Path.GetFileNameWithoutExtension(name);

            var item = new MapPreviewItem
            {
                Name = mapName,
                Map = battleMap,
                PreviewImage = await _previewRenderer.GeneratePreviewAsync(battleMap)
            };

            AvailableMaps.Add(item);
            SelectedTabIndex = 0;
            SelectMap(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading map from file");
        }
    }

    /// <summary>
    /// Loads available pre-existing maps from the resource provider
    /// </summary>
    internal async Task LoadAvailableMaps()
    {
        IsLoadingMaps = true;
        ClearAvailableMaps();
        try
        {
            var maps = await _mapResourceProvider.GetAvailableMapsAsync();
            foreach (var (name, hexData) in maps)
            {
                var battleMap = _mapFactory.CreateFromData(hexData);
                var item = new MapPreviewItem
                {
                    Name = name,
                    Map = battleMap,
                    // Generate preview
                    PreviewImage = await _previewRenderer.GeneratePreviewAsync(battleMap)
                };

                AvailableMaps.Add(item);
            }

            // Preselect the first item to avoid NREs
            if (AvailableMaps.Count > 0)
            {
                SelectMap(AvailableMaps[0]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading available maps");
        }
        finally
        {
            IsLoadingMaps = false;
        }
    }

    private async Task UpdateMap()
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

            GeneratedMap = _mapFactory.GenerateMap(MapWidth, MapHeight, generator);
            NotifyPropertyChanged(nameof(Map));

            // Update preview image based on the generated map
            var oldPreview = _previewImage as IDisposable;
            if (GeneratedMap != null)
            {
                PreviewImage = await _previewRenderer.GeneratePreviewAsync(GeneratedMap);
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
        ClearAvailableMaps();
        _previewSubscription?.Dispose();
        _mapParametersChanged.Dispose();
        GC.SuppressFinalize(this);
    }
    
    private void ClearAvailableMaps()
    {
        foreach (var item in AvailableMaps)
        {
            (item.PreviewImage as IDisposable)?.Dispose();
        }
        AvailableMaps.Clear();
    }
}

/// <summary>
/// Marker type for map parameter change events
/// </summary>
internal readonly struct MapParameterChange
{
    public static readonly MapParameterChange Instance = new();
}
