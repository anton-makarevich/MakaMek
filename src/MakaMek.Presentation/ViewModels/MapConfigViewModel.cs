using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using Microsoft.Extensions.Logging;
using Sanet.Localization;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Presentation.Models.Logger;
using Sanet.MakaMek.Presentation.ViewModels.Maps;
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
    private CancellationTokenSource? _updateCts;
    private readonly MapGenerationService _mapGeneration;
    private readonly AvailableMapsService _availableMaps;
    private readonly IDisposable? _previewSubscription;
    private readonly Subject<MapParameterChange> _mapParametersChanged = new();
    private readonly ILogger<MapConfigViewModel> _logger;
    private readonly ILocalizationService _localizationService;

    /// <summary>
    /// Creates the map config screen. The map services are resolved by the composition root; this
    /// screen only coordinates their results and the parameter state.
    /// </summary>
    public MapConfigViewModel(
        MapGenerationService mapGeneration,
        AvailableMapsService availableMaps,
        ILogger<MapConfigViewModel> logger,
        ILocalizationService localizationService)
    {
        _mapGeneration = mapGeneration;
        _availableMaps = availableMaps;
        _logger = logger;
        _localizationService = localizationService;

        LoadMapCommand = new AsyncCommand(LoadMap);

        // Subscribe with debouncing
        _previewSubscription = _mapParametersChanged
            .Throttle(TimeSpan.FromMilliseconds(300))
            .Subscribe( (_) =>
            {
                UpdateMap().SafeFireAndForget(ex => _logger.LogErrorUpdatingMap(ex));
            });

        // Generate an initial map and preview
        UpdateMap().SafeFireAndForget(ex => _logger.LogErrorGeneratingInitialMap(ex));
        
        // Load available pre-existing maps
        LoadAvailableMaps().SafeFireAndForget(ex => _logger.LogErrorLoadingAvailableMaps(ex));
    }

    public string MapWidthFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_Width_Formatted"), MapWidth);
    public string MapHeightFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_Height_Formatted"), MapHeight);
    public string ForestCoverageFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_ForestCoverage_Formatted"), ForestCoverage);
    public string LightWoodsFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_LightWoods_Formatted"), LightWoodsPercentage);
    public string HillCoverageFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_HillCoverage_Formatted"), HillCoverage);
    public string MaxElevationFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_MaxElevation_Formatted"), MaxElevation);
    public string RoughCoverageFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_RoughCoverage_Formatted"), RoughCoverage);
    public string LakeCoverageFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_LakeCoverage_Formatted"), LakeCoverage);
    public string LakeMaxDepthFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_LakeMaxDepth_Formatted"), LakeMaxDepth);
    public string RiverCountFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_RiverCount_Formatted"), RiverCount);
    public string RoadCountFormatted => string.Format(CultureInfo.InvariantCulture,
        _localizationService.GetString("MapConfig_RoadCount_Formatted"), RoadCount);

    /// <summary>
    /// Maximum allowed rough terrain coverage to ensure a hex cannot contain both woods and rough terrain.
    /// WoodCoverage + RoughCoverage cannot be greater than 100.
    /// </summary>
    public int MaxRoughCoverage => 100 - ForestCoverage;

    /// <summary>
    /// Maximum allowed forest coverage to ensure a hex cannot contain both woods and rough terrain.
    /// WoodCoverage + RoughCoverage cannot be greater than 100.
    /// </summary>
    public int MaxForestCoverage => 100 - RoughCoverage;

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
    /// Collection of pre-existing maps available for selection,
    /// with a trailing <see cref="LoadMapPlaceholder"/> sentinel
    /// </summary>
    public ObservableCollection<object> AvailableMaps { get; } = [];

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
            NotifyPropertyChanged(nameof(MapWidthFormatted));
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
            NotifyPropertyChanged(nameof(MapHeightFormatted));
            StartMapUpdate();
        }
    } = 17;

    public int ForestCoverage
    {
        get => _forestCoverage;
        set
        {
            SetProperty(ref _forestCoverage, value);
            NotifyPropertyChanged(nameof(HasForest));
            NotifyPropertyChanged(nameof(ForestCoverageFormatted));
            NotifyPropertyChanged(nameof(MaxRoughCoverage));
            NotifyPropertyChanged(nameof(RoughCoverageFormatted));
            StartMapUpdate();
        }
    }

    public int RoughCoverage
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(RoughCoverageFormatted));
            NotifyPropertyChanged(nameof(MaxForestCoverage));
            StartMapUpdate();
        }
    } = 10;

    public int LightWoodsPercentage
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(LightWoodsFormatted));
            StartMapUpdate();
        }
    } = 30;

    public int HillCoverage
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(HillCoverageFormatted));
            NotifyPropertyChanged(nameof(HasHills));
            StartMapUpdate();
        }
    }

    public int MaxElevation
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(MaxElevationFormatted));
            StartMapUpdate();
        }
    } = 2;

    public int LakeCoverage
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(LakeCoverageFormatted));
            NotifyPropertyChanged(nameof(HasLakes));
            StartMapUpdate();
        }
    }

    public int LakeMaxDepth
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(LakeMaxDepthFormatted));
            StartMapUpdate();
        }
    } = 2;

    public int RiverCount
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(RiverCountFormatted));
            StartMapUpdate();
        }
    }

    public int RoadCount
    {
        get;
        set
        {
            SetProperty(ref field, value);
            NotifyPropertyChanged(nameof(RoadCountFormatted));
            StartMapUpdate();
        }
    }

    public bool HasForest => _forestCoverage > 0;

    public bool HasHills => HillCoverage > 0;

    public bool HasLakes => LakeCoverage > 0;

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
        foreach (var map in AvailableMaps.OfType<MapPreviewItem>())
        {
            map.IsSelected = map == item;
        }
        SelectedMap = item;
    }

    public IAsyncCommand LoadMapCommand { get; }

    public IAsyncCommand ExportMapCommand => field ??= new AsyncCommand(async () =>
    {
        if (Map == null) return;
        await _availableMaps.Export(Map, _localizationService.GetString("EditMap_ExportMapDialogTitle"));
    });

    private async Task LoadMap()
    {
        try
        {
            var item = await _availableMaps.LoadFromFileAsync("Select Map File");
            if (item is null)
            {
                return;
            }

            var placeholder = AvailableMaps.OfType<LoadMapPlaceholder>().FirstOrDefault();
            if (placeholder is not null)
            {
                var idx = AvailableMaps.IndexOf(placeholder);
                AvailableMaps.Insert(idx, item);
            }
            else
            {
                AvailableMaps.Add(item);
            }
            SelectedTabIndex = 0;
            SelectMap(item);
        }
        catch (Exception ex)
        {
            _logger.LogErrorLoadingMapFromFile(ex);
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
            var items = await _availableMaps.GetAvailableMapsAsync();
            var tasks = new List<Task>(items.Count);

            foreach (var item in items)
            {
                AvailableMaps.Add(item);

                // Create preview generation task
                tasks.Add(_availableMaps.RenderPreviewAsync(item));
            }

            // Execute all preview generation in parallel
            await Task.WhenAll(tasks);

            // Append the "Load Map" placeholder card sentinel at the end
            AvailableMaps.Add(new LoadMapPlaceholder());

            // Preselect the first item to avoid NREs - UI appears instantly
            var firstMap = AvailableMaps.OfType<MapPreviewItem>().FirstOrDefault();
            if (firstMap is not null)
            {
                SelectMap(firstMap);
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorLoadingAvailableMaps(ex);
        }
        finally
        {
            IsLoadingMaps = false;
        }
    }

    private async Task UpdateMap()
    {
        // Cancel any previous update operation
        if (_updateCts is not null)
        {
            await _updateCts.CancelAsync();
            _updateCts?.Dispose();
        }

        _updateCts = new CancellationTokenSource();
        var token = _updateCts.Token;

        try
        {
            GeneratedMap = _mapGeneration.Generate(CreateGenerationSettings());
            NotifyPropertyChanged(nameof(Map));

            // Update preview image based on the generated map
            var oldPreview = _previewImage as IDisposable;
            if (GeneratedMap != null)
            {
                var newPreview = await _mapGeneration.RenderPreviewAsync(GeneratedMap, token);

                // Only update if not canceled and preview was generated
                if (!token.IsCancellationRequested && newPreview != null)
                {
                    PreviewImage = newPreview;
                    oldPreview?.Dispose();
                }
                else
                {
                    // Dispose the new preview if we got one but were canceled
                    (newPreview as IDisposable)?.Dispose();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer update cancels this one - ignore gracefully
        }
        finally
        {
            // Only clear the flag if this operation wasn't canceled
            if (!token.IsCancellationRequested)
            {
                IsGenerating = false;
            }
        }
    }

    private MapGenerationSettings CreateGenerationSettings() => new(
        MapWidth,
        MapHeight,
        ForestCoverage,
        LightWoodsPercentage,
        RoughCoverage,
        HillCoverage,
        MaxElevation,
        LakeCoverage,
        LakeMaxDepth,
        RiverCount,
        RoadCount);

    public void Dispose()
    {
        _updateCts?.Cancel();
        _updateCts?.Dispose();
        _updateCts = null;

        (_previewImage as IDisposable)?.Dispose();
        ClearAvailableMaps();
        _previewSubscription?.Dispose();
        _mapParametersChanged.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ClearAvailableMaps()
    {
        foreach (var item in AvailableMaps.OfType<MapPreviewItem>())
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
    public static readonly MapParameterChange Instance;
}
