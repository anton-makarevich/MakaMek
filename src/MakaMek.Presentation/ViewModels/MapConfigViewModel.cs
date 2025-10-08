using System.Reactive.Linq;
using System.Reactive.Subjects;
using Sanet.MakaMek.Core.Services;
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
    private readonly IMapPreviewRenderer _previewRenderer;
    private readonly IDisposable? _previewSubscription;
    private readonly Subject<MapParameterChange> _mapParametersChanged = new();

    public MapConfigViewModel(IMapPreviewRenderer previewRenderer)
    {
        _previewRenderer = previewRenderer;


        // Subscribe with 3-second debounce
        _previewSubscription = _mapParametersChanged
            .Throttle(TimeSpan.FromSeconds(3))
            .Subscribe(_ => UpdatePreview());

        // Generate initial preview
        UpdatePreview();

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
            _mapParametersChanged.OnNext(MapParameterChange.Instance);
        }
    }

    public int MapHeight
    {
        get => _mapHeight;
        set
        {
            SetProperty(ref _mapHeight, value);
            _mapParametersChanged.OnNext(MapParameterChange.Instance);
        }
    }

    public int ForestCoverage
    {
        get => _forestCoverage;
        set
        {
            SetProperty(ref _forestCoverage, value);
            NotifyPropertyChanged(nameof(IsLightWoodsEnabled));
            _mapParametersChanged.OnNext(MapParameterChange.Instance);
        }
    }

    public int LightWoodsPercentage
    {
        get => _lightWoodsPercentage;
        set
        {
            SetProperty(ref _lightWoodsPercentage, value);
            _mapParametersChanged.OnNext(MapParameterChange.Instance);
        }
    }

    public bool IsLightWoodsEnabled => _forestCoverage > 0;

    public object? PreviewImage
    {
        get => _previewImage;
        private set => SetProperty(ref _previewImage, value);
    }

    private void UpdatePreview()
    {
        PreviewImage = _previewRenderer.GeneratePreview(
            MapWidth,
            MapHeight,
            ForestCoverage,
            LightWoodsPercentage);
    }

    public void Dispose()
    {
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

