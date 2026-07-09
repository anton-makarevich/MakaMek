using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Presentation.ViewModels;

namespace Sanet.MakaMek.Avalonia.Controls;

public class HexRenderControl : Control
{
    private readonly ILogger _logger;
    private readonly ILocalizationService? _localizationService;
    private readonly DecodedBitmapCache _bitmapCache;
    private readonly IScheduler _scheduler;
    private readonly Dictionary<HexCoordinates, HexRenderData> _hexData = new();
    private HexRenderConfiguration _configuration = HexRenderConfiguration.Default;
    private IReadOnlyDictionary<HexCoordinates, HighlightBoundaryOutline> _boundaryOutlines =
        new Dictionary<HexCoordinates, HighlightBoundaryOutline>();

    private bool _invalidateQueued;
    private readonly List<IDisposable> _subscriptions = [];

    private readonly Geometry _hexPolygon;
    private readonly Point[] _cornerPoints;

    public HexRenderControl(
        ILogger logger,
        ITerrainAssetService terrainAssetService,
        ILocalizationService? localizationService,
        IScheduler? scheduler)
    {
        _logger = logger;
        _localizationService = localizationService;
        _scheduler = scheduler ?? new SynchronizationContextScheduler(SynchronizationContext.Current!);
        _bitmapCache = new DecodedBitmapCache(terrainAssetService, QueueInvalidate);

        var corners = HexagonGeometry.GetCorners();
        _cornerPoints = corners.Select(c => new Point(c.X, c.Y)).ToArray();

        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(_cornerPoints[0], true);
            for (var i = 1; i < _cornerPoints.Length; i++)
                ctx.LineTo(_cornerPoints[i]);
            ctx.EndFigure(true);
        }
        _hexPolygon = sg;
    }

    public void SetHexData(IEnumerable<HexRenderData> data, HexRenderConfiguration configuration)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        _hexData.Clear();

        _configuration = configuration;

        foreach (var item in data)
        {
            _hexData[item.Hex.Coordinates] = item;
            SubscribeToHex(item.Hex);
        }

        InvalidateVisual();
    }

    public void SetBoundaryOutlines(IReadOnlyDictionary<HexCoordinates, HighlightBoundaryOutline>? outlines)
    {
        _boundaryOutlines = outlines ?? new Dictionary<HexCoordinates, HighlightBoundaryOutline>();
        InvalidateVisual();
    }

    public void UpdateConfiguration(HexRenderConfiguration configuration)
    {
        _configuration = configuration;
        InvalidateVisual();
    }

    public async Task PrefetchAllBitmapsAsync(ITerrainAssetService assetService)
    {
        var tasks = new List<Task>();
        foreach (var (_, data) in _hexData)
        {
            var hex = data.Hex;
            var biome = hex.Biome;
            tasks.Add(PrefetchBaseAsync(assetService, biome));
            tasks.Add(PrefetchEdgesAsync(assetService, biome, data.Edges, hex.Coordinates));
            tasks.Add(PrefetchWaterAsync(assetService, biome, data.WaterBitmask));
            tasks.Add(PrefetchRoadAsync(assetService, biome, data.RoadBitmask));
            foreach (var terrain in hex.GetTerrains())
            {
                if (terrain.Id == MakaMekTerrains.Clear) continue;
                tasks.Add(PrefetchOverlayAsync(assetService, biome, terrain.Id));
            }
        }
        await Task.WhenAll(tasks);
    }

    private async Task PrefetchBaseAsync(ITerrainAssetService assetService, string biome)
    {
        var key = DecodedBitmapCache.BaseKey(biome);
        if (_bitmapCache.IsCached(key)) return;
        var bytes = await assetService.GetBaseBiomeImage(biome);
        if (bytes != null)
        {
            using var stream = new MemoryStream(bytes);
            _bitmapCache.SetCached(key, new Bitmap(stream));
        }
    }

    private async Task PrefetchEdgesAsync(ITerrainAssetService assetService, string biome,
        IReadOnlyList<HexEdge> edges, HexCoordinates coords)
    {
        foreach (var edge in edges)
        {
            if (edge.ElevationDifference == 0) continue;
            var edgeType = edge.ElevationDifference > 0 ? TerrainAssetType.EdgeTop : TerrainAssetType.EdgeBottom;
            var key = DecodedBitmapCache.EdgeKey(biome, edge.Direction, edgeType.ToString(), coords.Q, coords.R);
            if (_bitmapCache.IsCached(key)) continue;
            var bytes = await assetService.GetEdgeImage(biome, edge.Direction, edgeType, coords);
            if (bytes != null)
            {
                using var stream = new MemoryStream(bytes);
                _bitmapCache.SetCached(key, new Bitmap(stream));
            }
        }
    }

    private async Task PrefetchWaterAsync(ITerrainAssetService assetService, string biome,
        CanonicalBitmaskResult? water)
    {
        if (water == null) return;
        var key = DecodedBitmapCache.WaterKey(biome, water.CanonicalMask, water.RotationSteps);
        if (_bitmapCache.IsCached(key)) return;
        var bytes = await assetService.GetWaterTextureImage(biome, water);
        if (bytes != null)
        {
            using var stream = new MemoryStream(bytes);
            _bitmapCache.SetCached(key, new Bitmap(stream));
        }
    }

    private async Task PrefetchRoadAsync(ITerrainAssetService assetService, string biome,
        CanonicalBitmaskResult? road)
    {
        if (road == null) return;
        var key = DecodedBitmapCache.RoadKey(biome, road.CanonicalMask, road.RotationSteps);
        if (_bitmapCache.IsCached(key)) return;
        var bytes = await assetService.GetRoadTextureImage(biome, road);
        if (bytes != null)
        {
            using var stream = new MemoryStream(bytes);
            _bitmapCache.SetCached(key, new Bitmap(stream));
        }
    }

    private async Task PrefetchOverlayAsync(ITerrainAssetService assetService, string biome,
        MakaMekTerrains terrainId)
    {
        var terrainType = terrainId.ToString().ToLowerInvariant();
        var key = DecodedBitmapCache.OverlayKey(biome, terrainType);
        if (_bitmapCache.IsCached(key)) return;
        var bytes = await assetService.GetTerrainOverlayImage(biome, terrainType);
        if (bytes != null)
        {
            using var stream = new MemoryStream(bytes);
            _bitmapCache.SetCached(key, new Bitmap(stream));
        }
    }

    private void SubscribeToHex(Hex hex)
    {
        _subscriptions.Add(hex.HighlightsChanged
            .ObserveOn(_scheduler)
            .Subscribe(_ => QueueInvalidate()));

        _subscriptions.Add(hex.TerrainsChanged
            .ObserveOn(_scheduler)
            .Subscribe(_ => QueueInvalidate()));
    }

    private void QueueInvalidate()
    {
        lock (this)
        {
            if (_invalidateQueued) return;
            _invalidateQueued = true;
        }

        Dispatcher.UIThread.Post(() =>
        {
            lock (this) { _invalidateQueued = false; }
            InvalidateVisual();
        }, DispatcherPriority.Render);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        foreach (var sub in _subscriptions)
            sub.Dispose();
        _subscriptions.Clear();
        _bitmapCache.Clear();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        var config = _configuration;
        var hexW = HexCoordinatesPixelExtensions.HexWidth;
        var hexH = HexCoordinatesPixelExtensions.HexHeight;

        var outlinePen = new Pen(Brushes.White, 1);
        var allDirections = HexDirectionExtensions.AllDirections;

        foreach (var (coords, data) in _hexData)
        {
            var ox = coords.H;
            var oy = coords.V;
            var hex = data.Hex;

            using (context.PushTransform(Matrix.CreateTranslation(ox, oy)))
            {
                // 1. Base biome
                var baseKey = DecodedBitmapCache.BaseKey(hex.Biome);
                var baseBitmap = _bitmapCache.GetOrSchedule(baseKey,
                    () => _bitmapCache.AssetService.GetBaseBiomeImage(hex.Biome));
                if (baseBitmap != null)
                    context.DrawImage(baseBitmap, new Rect(0, 0, hexW, hexH));

                // 2. Elevation edges
                foreach (var edge in data.Edges)
                {
                    if (edge.ElevationDifference == 0) continue;
                    var edgeType = edge.ElevationDifference > 0
                        ? TerrainAssetType.EdgeTop
                        : TerrainAssetType.EdgeBottom;
                    var edgeKey = DecodedBitmapCache.EdgeKey(hex.Biome, edge.Direction,
                        edgeType.ToString(), coords.Q, coords.R);
                    var edgeBitmap = _bitmapCache.GetOrSchedule(edgeKey,
                        () => _bitmapCache.AssetService.GetEdgeImage(hex.Biome, edge.Direction, edgeType, coords));
                    if (edgeBitmap != null)
                        context.DrawImage(edgeBitmap, new Rect(0, 0, hexW, hexH));
                }

                // 3. Water (rotated)
                if (data.WaterBitmask != null)
                {
                    var waterKey = DecodedBitmapCache.WaterKey(hex.Biome,
                        data.WaterBitmask.CanonicalMask, data.WaterBitmask.RotationSteps);
                    var waterBitmap = _bitmapCache.GetOrSchedule(waterKey,
                        () => _bitmapCache.AssetService.GetWaterTextureImage(hex.Biome, data.WaterBitmask!));
                    if (waterBitmap != null)
                        DrawRotatedBitmap(context, waterBitmap, hexW, hexH,
                            -data.WaterBitmask.RotationSteps * 60.0);
                }

                // 4. Terrain overlays
                foreach (var terrain in hex.GetTerrains())
                {
                    if (terrain.Id == MakaMekTerrains.Clear) continue;
                    var terrainType = terrain.Id.ToString().ToLowerInvariant();
                    var overlayKey = DecodedBitmapCache.OverlayKey(hex.Biome, terrainType);
                    var overlayBitmap = _bitmapCache.GetOrSchedule(overlayKey,
                        () => _bitmapCache.AssetService.GetTerrainOverlayImage(hex.Biome, terrainType));
                    if (overlayBitmap != null)
                        context.DrawImage(overlayBitmap, new Rect(0, 0, hexW, hexH));
                }

                // 5. Road (skipped when Rubble)
                if (!hex.HasTerrain(MakaMekTerrains.Rubble) && data.RoadBitmask != null)
                {
                    var roadKey = DecodedBitmapCache.RoadKey(hex.Biome,
                        data.RoadBitmask.CanonicalMask, data.RoadBitmask.RotationSteps);
                    var roadBitmap = _bitmapCache.GetOrSchedule(roadKey,
                        () => _bitmapCache.AssetService.GetRoadTextureImage(hex.Biome, data.RoadBitmask!));
                    if (roadBitmap != null)
                        DrawRotatedBitmap(context, roadBitmap, hexW, hexH,
                            -data.RoadBitmask.RotationSteps * 60.0);
                }

                // 6. Hex polygon outline
                if (config.ShowOutline)
                    context.DrawGeometry(null, outlinePen, _hexPolygon);

                // 7. Highlight fills/strokes ordered by RenderOrder
                var highlights = hex.Highlights.OrderBy(h => h.RenderOrder).ToList();
                foreach (var highlight in highlights)
                {
                    var (stroke, fill) = GetHighlightBrushes(highlight);
                    if (fill != null || stroke != null)
                        context.DrawGeometry(fill, stroke != null ? new Pen(stroke, 1) : null, _hexPolygon);
                }

                // 8. Boundary outlines
                if (_boundaryOutlines.TryGetValue(coords, out var boundary) && boundary.EdgeMask != 0)
                {
                    var boundaryBrush = ParseBrush(boundary.Color);
                    var bp = new Pen(boundaryBrush, boundary.Thickness);
                    for (var i = 0; i < allDirections.Length; i++)
                    {
                        if ((boundary.EdgeMask & (1 << i)) == 0) continue;
                        var (startIdx, endIdx) = allDirections[i].GetHexPointEdgeCornerIndices();
                        context.DrawLine(bp, _cornerPoints[startIdx], _cornerPoints[endIdx]);
                    }
                }

                // 9a. Coordinate label (top-center)
                if (config.ShowLabels)
                {
                    var coordText = new FormattedText(
                        coords.ToString(),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        12,
                        Brushes.White);
                    var coordX = (hexW - coordText.Width) / 2;
                    context.DrawText(coordText, new Point(coordX, 2));
                }

                // 9b. Terrain info label (bottom-center)
                if (config.ShowLabels)
                {
                    var labelContent = GenerateLabelContent(hex);
                    if (labelContent != null)
                    {
                        var infoText = new FormattedText(
                            labelContent,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            Typeface.Default,
                            11,
                            Brushes.White);
                        var infoX = (hexW - infoText.Width) / 2;
                        var infoY = hexH - infoText.Height - 2;
                        context.DrawText(infoText, new Point(infoX, infoY));
                    }
                }

                // 9c. Highlight text (center)
                if (config.ShowHighlightLabels && highlights.Count > 0 && _localizationService != null)
                {
                    var highest = highlights[^1];
                    var hlText = highest.Render(_localizationService);
                    var hlFormatted = new FormattedText(
                        hlText,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        9,
                        Brushes.White)
                    {
                        MaxTextWidth = hexW - 10,
                        MaxTextHeight = hexH - 10,
                        Trimming = TextTrimming.CharacterEllipsis
                    };
                    var hlX = (hexW - hlFormatted.Width) / 2;
                    var hlY = (hexH - hlFormatted.Height) / 2;
                    context.DrawText(hlFormatted, new Point(hlX, hlY));
                }
            }
        }
    }

    private static void DrawRotatedBitmap(DrawingContext context, Bitmap bitmap,
        double hexW, double hexH, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        using (context.PushTransform(Matrix.CreateRotation(radians)))
        {
            context.DrawImage(bitmap, new Rect(0, 0, hexW, hexH));
        }
    }

    private static (IBrush? Stroke, IBrush? Fill) GetHighlightBrushes(IHexHighlightType highlight)
    {
        return highlight switch
        {
            MovementReachableHighlight => (
                new SolidColorBrush(Color.Parse("#00BFFF")),
                new SolidColorBrush(Color.Parse("#3300BFFF"))),
            AttackReachableHighlight => (
                new SolidColorBrush(Color.Parse("#FFB347")),
                new SolidColorBrush(Color.Parse("#33FFB347"))),
            LosBlockingHighlight => (
                new SolidColorBrush(Color.Parse("#8B0000")),
                new SolidColorBrush(Color.Parse("#338B0000"))),
            _ => (Brushes.White, null)
        };
    }

    private static IBrush ParseBrush(string colorHex)
    {
        if (Color.TryParse(colorHex, out var color))
            return new SolidColorBrush(color);
        return Brushes.White;
    }

    private static string? GenerateLabelContent(Hex hex)
    {
        var waterDepth = hex.GetWaterDepth();
        var bridgeHeight = hex.GetBridgeHeight();

        var abbreviated = new List<string>(3);
        if (hex.Level != 0) abbreviated.Add($"L{hex.Level}");
        if (waterDepth.HasValue) abbreviated.Add($"D{waterDepth.Value}");
        if (bridgeHeight.HasValue) abbreviated.Add($"B{bridgeHeight.Value}");

        return abbreviated.Count switch
        {
            0 => null,
            1 when hex.Level != 0 => $"LEVEL {hex.Level}",
            1 when waterDepth.HasValue => $"DEPTH {waterDepth.Value}",
            1 when bridgeHeight.HasValue => $"BRIDGE {bridgeHeight.Value}",
            _ => string.Join(" ", abbreviated)
        };
    }

    public DecodedBitmapCache BitmapCache => _bitmapCache;
}
