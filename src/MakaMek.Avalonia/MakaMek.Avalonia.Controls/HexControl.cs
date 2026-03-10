using System.Reactive.Linq;
using AsyncAwaitBestPractices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Avalonia.Controls;

public class HexControl : Panel
{
    private readonly Polygon _hexPolygon;
    private readonly ITerrainAssetService _terrainAssetService;
    private readonly ILogger _logger;
    private readonly Hex _hex;
    private IReadOnlyList<HexEdge>? _edges;
    private readonly List<Image> _terrainImageLayers = [];

    private static readonly IBrush DefaultStroke = Brushes.White;
    private static readonly IBrush HighlightStroke = new SolidColorBrush(Color.Parse("#00BFFF")); // Light blue
    private static readonly IBrush HighlightFill = new SolidColorBrush(Color.Parse("#3300BFFF")); // Semi-transparent light blue
    private static readonly IBrush TransparentFill = Brushes.Transparent;

    private const double DefaultStrokeThickness = 2;
    private const double HighlightStrokeThickness = 3;

    // Z-index constants for layer ordering
    private const int ZIndexBaseTerrain = 0;
    private const int ZIndexEdgeEffects = 10;
    private const int ZIndexOverlays = 20;
    private const int ZIndexPolygon = 30;
    private const int ZIndexLabel = 31;

    private readonly IDisposable? _hexSubscription;

    private static Points GetHexPoints()
    {
        const double width = HexCoordinatesPixelExtensions.HexWidth;
        const double height = HexCoordinatesPixelExtensions.HexHeight;

        return new Points([
            new Point(0, height * 0.5),           // Left
            new Point(width * 0.25, height),      // Bottom Left
            new Point(width * 0.75, height),      // Bottom Right
            new Point(width, height * 0.5),       // Right
            new Point(width * 0.75, 0),           // Top Right
            new Point(width * 0.25, 0)            // Top Left
        ]);
    }

    public HexControl(Hex hex, ILogger logger,  ITerrainAssetService terrainAssetService,
        IReadOnlyList<HexEdge>? edges = null)
    {
        _hex = hex;
        _terrainAssetService = terrainAssetService;
        
        _logger = logger;
        _edges = edges?.ToArray();
        Width = HexCoordinatesPixelExtensions.HexWidth;
        Height = HexCoordinatesPixelExtensions.HexHeight;

        // Hex polygon (top layer)
        _hexPolygon = new Polygon
        {
            Points = GetHexPoints(),
            Fill = TransparentFill,
            Stroke = DefaultStroke,
            StrokeThickness = DefaultStrokeThickness
        };

        var label = new Label
        {
            Content = hex.Coordinates.ToString(),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brushes.White
        };

        // Add polygon and label (always on top)
        Children.Add(_hexPolygon);
        _hexPolygon.ZIndex = ZIndexPolygon;
        Children.Add(label);
        label.ZIndex = ZIndexLabel;

        // Set initial highlight state
        if (_hex.IsHighlighted)
            Highlight(HexHighlightType.Selected);

        // Subscribe to highlight changes from the Hex model
        _hexSubscription = _hex.IsHighlightedChanged
            .ObserveOn(SynchronizationContext.Current!) // Ensure events are processed on the UI thread
            .Subscribe(isHighlighted =>
                Highlight(isHighlighted ? HexHighlightType.Selected : HexHighlightType.None));

        // Set position
        SetValue(Canvas.LeftProperty, hex.Coordinates.H);
        SetValue(Canvas.TopProperty, hex.Coordinates.V);

        Render().SafeFireAndForget(ex => logger.LogError(ex, "Error rendering hex at {Q},{R}",
            hex.Coordinates.Q, hex.Coordinates.R));
    }

    public Hex Hex => _hex;

    private void Highlight(HexHighlightType type)
    {
        switch (type)
        {
            case HexHighlightType.Selected:
                _hexPolygon.Stroke = HighlightStroke;
                _hexPolygon.StrokeThickness = HighlightStrokeThickness;
                _hexPolygon.Fill = HighlightFill;
                break;
            case HexHighlightType.None:
            default:
                _hexPolygon.Stroke = DefaultStroke;
                _hexPolygon.StrokeThickness = DefaultStrokeThickness;
                _hexPolygon.Fill = TransparentFill;
                break;
        }
    }

    private void ClearImageLayers()
    {
        foreach (var layer in _terrainImageLayers)
        {
            Children.Remove(layer);
        }

        _terrainImageLayers.Clear();
    }

    private void AddImageLayer(Bitmap? image, int zIndex)
    {
        if (image == null) return;

        var imageControl = new Image
        {
            Width = Width,
            Height = Height,
            Stretch = Stretch.Fill,
            Source = image
        };

        Children.Add(imageControl);
        imageControl.ZIndex = zIndex;
        _terrainImageLayers.Add(imageControl);
    }

    private static Bitmap? BytesToBitmap(byte[]? bytes)
    {
        if (bytes == null) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    private async Task UpdateBaseTerrainImage()
    {
        var biomeId = _hex.Biome;
        var imageBytes = await _terrainAssetService.GetBaseBiomeImage(biomeId);
        var bitmap = BytesToBitmap(imageBytes);
        AddImageLayer(bitmap, ZIndexBaseTerrain);
    }

    private async Task UpdateEdgeLayers()
    {
        if (_edges == null) return;
        var biomeId = _hex.Biome;
        var edgeIndex = 0;

        foreach (var edge in _edges)
        {
            if (edge.ElevationDifference == 0) continue;

            var edgeType = edge.ElevationDifference > 0
                ? TerrainAssetType.EdgeTop
                : TerrainAssetType.EdgeBottom;

            var imageBytes = await _terrainAssetService.GetEdgeImage(
                biomeId, edge.Direction, edgeType, _hex.Coordinates);
            var bitmap = BytesToBitmap(imageBytes);
            AddImageLayer(bitmap, ZIndexEdgeEffects + edgeIndex);
            edgeIndex++;
        }
    }

    private async Task UpdateOverlayLayers()
    {
        var biomeId = _hex.Biome;
        var overlayIndex = 0;

        foreach (var terrain in _hex.GetTerrains())
        {
            if (terrain.Id == MakaMekTerrains.Clear) continue;

            var terrainType = terrain.Id.ToString().ToLower();
            var imageBytes = await _terrainAssetService.GetTerrainOverlayImage(biomeId, terrainType);
            var bitmap = BytesToBitmap(imageBytes);
            AddImageLayer(bitmap, ZIndexOverlays + overlayIndex);
            overlayIndex++;
        }
    }

    public bool IsPointInside(Point point)
    {
        // Transform the point from global coordinates to local control coordinates
        var localPoint = point - new Point(Bounds.X, Bounds.Y);

        // Check if the local point is inside the hex polygon using the ray casting algorithm
        return IsPointInPolygon(localPoint, _hexPolygon.Points);
    }

    private static bool IsPointInPolygon(Point point, IList<Point> polygonPoints)
    {
        if (polygonPoints.Count < 3) return false;

        var inside = false;
        var j = polygonPoints.Count - 1;

        for (var i = 0; i < polygonPoints.Count; i++)
        {
            var pi = polygonPoints[i];
            var pj = polygonPoints[j];

            if (pi.Y > point.Y != pj.Y > point.Y &&
                point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X)
            {
                inside = !inside;
            }

            j = i;
        }

        return inside;
    }

    public async Task Render()
    {
        ClearImageLayers();
        await UpdateBaseTerrainImage();
        await UpdateEdgeLayers();
        await UpdateOverlayLayers();
    }

    /// <summary>
    /// Updates the edge data and triggers a re-render
    /// </summary>
    /// <param name="edges">New edge data for the hex</param>
    public void UpdateEdges(IReadOnlyList<HexEdge> edges)
    {
        _edges = edges;
        Render().SafeFireAndForget(ex => _logger.LogError(ex, "Error rendering hex at {Q},{R}",
            _hex.Coordinates.Q, _hex.Coordinates.R));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _hexSubscription?.Dispose();
        base.OnDetachedFromVisualTree(e);
    }
}
