using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
    private readonly ILocalizationService? _localizationService;
    private readonly DecodedBitmapCache _bitmapCache;
    private readonly IScheduler _scheduler;
    private readonly Dictionary<HexCoordinates, HexRenderData> _hexData = new();
    private HexRenderConfiguration _configuration = HexRenderConfiguration.Default;
    private IReadOnlyDictionary<HexCoordinates, HighlightBoundaryOutline> _boundaryOutlines =
        new Dictionary<HexCoordinates, HighlightBoundaryOutline>();

    private bool _invalidateQueued;
    private readonly Lock _syncLock = new();
    private readonly Dictionary<HexCoordinates, IDisposable> _subscriptions = [];
    private List<HexCoordinates> _sortedCoords = [];
    private readonly Dictionary<HexCoordinates, FormattedText> _coordLabelCache = new();
    private readonly Dictionary<HexCoordinates, FormattedText?> _terrainLabelCache = new();
    private readonly Dictionary<HexCoordinates, List<IHexHighlightType>> _sortedHighlightsCache = new();
    private readonly Dictionary<(string Color, double Thickness), Pen> _boundaryPenCache = new();

    private readonly Pen _whiteOutlinePen;
    private readonly Pen _whiteHighlightPen;
    private readonly IBrush _whiteHighlightBrush;
    private readonly (Pen Pen, IBrush Fill) _movementHighlight;
    private readonly (Pen Pen, IBrush Fill) _attackHighlight;
    private readonly (Pen Pen, IBrush Fill) _losBlockingHighlight;

    private readonly Geometry _hexPolygon;
    private readonly Point[] _cornerPoints;
    private readonly List<TaskCompletionSource> _renderTcs = [];

    public HexRenderControl(ITerrainAssetService terrainAssetService,
        ILocalizationService? localizationService,
        IScheduler? scheduler,
        IAvaloniaResourcesLocator? resourcesLocator)
    {
        _localizationService = localizationService;
        _scheduler = scheduler ?? new SynchronizationContextScheduler(SynchronizationContext.Current!);
        _bitmapCache = new DecodedBitmapCache(terrainAssetService, QueueInvalidate);

        _whiteOutlinePen = new Pen(FindBrush(resourcesLocator, "WhiteHighlightBrush", Brushes.White));
        _whiteHighlightPen = new Pen(FindBrush(resourcesLocator, "WhiteHighlightBrush", Brushes.White));
        _whiteHighlightBrush = FindBrush(resourcesLocator, "WhiteHighlightBrush", Brushes.White);

        var movementStroke = FindBrush(resourcesLocator, "MovementReachableStrokeBrush", new SolidColorBrush(Color.Parse("#00BFFF")));
        var movementFill = FindBrush(resourcesLocator, "MovementReachableFillBrush", new SolidColorBrush(Color.Parse("#3300BFFF")));
        _movementHighlight = (new Pen(movementStroke), movementFill);

        var attackStroke = FindBrush(resourcesLocator, "AttackReachableStrokeBrush", new SolidColorBrush(Color.Parse("#FFB347")));
        var attackFill = FindBrush(resourcesLocator, "AttackReachableFillBrush", new SolidColorBrush(Color.Parse("#33FFB347")));
        _attackHighlight = (new Pen(attackStroke), attackFill);

        var losStroke = FindBrush(resourcesLocator, "LosBlockingStrokeBrush", new SolidColorBrush(Color.Parse("#8B0000")));
        var losFill = FindBrush(resourcesLocator, "LosBlockingFillBrush", new SolidColorBrush(Color.Parse("#338B0000")));
        _losBlockingHighlight = (new Pen(losStroke), losFill);

        var corners = HexagonGeometry.GetCorners();
        _cornerPoints = corners.Select(c => new Point(c.X, c.Y)).ToArray();

        var sg = new StreamGeometry();
        using (var ctx = sg.Open())
        {
            ctx.BeginFigure(_cornerPoints[0]);
            for (var i = 1; i < _cornerPoints.Length; i++)
                ctx.LineTo(_cornerPoints[i]);
            ctx.EndFigure(true);
        }
        _hexPolygon = sg;
    }

    public void SetHexData(IEnumerable<HexRenderData> data, HexRenderConfiguration configuration)
    {
        foreach (var sub in _subscriptions.Values)
            sub.Dispose();
        _subscriptions.Clear();
        _hexData.Clear();

        _configuration = configuration;

        foreach (var item in data)
        {
            _hexData[item.Hex.Coordinates] = item;
            _subscriptions[item.Hex.Coordinates] = SubscribeToHex(item.Hex);
        }

        _sortedCoords = _hexData.Keys
            .OrderBy(c => c.V)
            .ThenBy(c => c.H)
            .ToList();

        _coordLabelCache.Clear();
        foreach (var (coords, _) in _hexData)
        {
            _coordLabelCache[coords] = CreateCoordLabel(coords);
        }

        _terrainLabelCache.Clear();
        _sortedHighlightsCache.Clear();

        InvalidateVisual();
    }

    /// <summary>
    /// Replaces or inserts individual hex entries without rebuilding the whole renderer.
    /// Existing subscriptions for updated coordinates are disposed and recreated.
    /// New coordinates are inserted into <c>_sortedCoords</c> in (V, H) order via binary search.
    /// Caches for the affected coordinates are invalidated, and a redraw is queued.
    /// </summary>
    public void UpdateHexEntries(IEnumerable<HexRenderData> data)
    {
        foreach (var item in data)
        {
            var coord = item.Hex.Coordinates;
            var isNew = !_hexData.ContainsKey(coord);

            // Dispose the old subscription if this coordinate already exists
            if (_subscriptions.TryGetValue(coord, out var oldSub))
                oldSub.Dispose();

            _hexData[coord] = item;
            _subscriptions[coord] = SubscribeToHex(item.Hex);

            // Invalidate caches for this coordinate
            _coordLabelCache[coord] = CreateCoordLabel(coord);
            _terrainLabelCache.Remove(coord);
            _sortedHighlightsCache.Remove(coord);

            if (isNew)
            {
                // Binary-search insert to maintain (V, H) order
                var insertIndex = _sortedCoords.BinarySearch(coord, HexRenderOrderComparer.Instance);
                if (insertIndex < 0) insertIndex = ~insertIndex;
                _sortedCoords.Insert(insertIndex, coord);
            }
        }

        QueueInvalidate();
    }

    public void SetBoundaryOutlines(IReadOnlyDictionary<HexCoordinates, HighlightBoundaryOutline>? outlines)
    {
        _boundaryOutlines = outlines ?? new Dictionary<HexCoordinates, HighlightBoundaryOutline>();
        _boundaryPenCache.Clear();
        foreach (var (_, boundary) in _boundaryOutlines)
        {
            var key = (boundary.Color, boundary.Thickness);
            if (!_boundaryPenCache.ContainsKey(key))
                _boundaryPenCache[key] = new Pen(ParseBrush(boundary.Color), boundary.Thickness);
        }
        InvalidateVisual();
    }

    public void UpdateConfiguration(HexRenderConfiguration configuration)
    {
        _configuration = configuration;
        InvalidateVisual();
    }

    public async Task PrefetchAllBitmapsAsync(ITerrainAssetService assetService)
    {
        var enqueuedKeys = new HashSet<string>();
        var tasks = new List<Task>();
        foreach (var (_, (hex, readOnlyList, canonicalBitmaskResult, roadBitmask)) in _hexData)
        {
            var biome = hex.Biome;

            if (enqueuedKeys.Add(DecodedBitmapCache.BaseKey(biome)))
                tasks.Add(PrefetchBaseAsync(assetService, biome));

            tasks.Add(PrefetchEdgesAsync(assetService, biome, readOnlyList, hex.Coordinates));

            if (canonicalBitmaskResult != null)
            {
                var key = DecodedBitmapCache.WaterKey(biome,
                    canonicalBitmaskResult.CanonicalMask, canonicalBitmaskResult.RotationSteps);
                if (enqueuedKeys.Add(key))
                    tasks.Add(PrefetchWaterAsync(assetService, biome, canonicalBitmaskResult));
            }

            if (roadBitmask != null)
            {
                var key = DecodedBitmapCache.RoadKey(biome,
                    roadBitmask.CanonicalMask, roadBitmask.RotationSteps);
                if (enqueuedKeys.Add(key))
                    tasks.Add(PrefetchRoadAsync(assetService, biome, roadBitmask));
            }

            foreach (var terrain in hex.GetTerrains())
            {
                if (terrain.Id == MakaMekTerrains.Clear) continue;
                var key = DecodedBitmapCache.OverlayKey(biome,
                    terrain.Id.ToString().ToLowerInvariant());
                if (enqueuedKeys.Add(key))
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

    private IDisposable SubscribeToHex(Hex hex)
    {
        var sub1 = hex.HighlightsChanged
            .ObserveOn(_scheduler)
            .Subscribe(_ =>
            {
                _sortedHighlightsCache.Remove(hex.Coordinates);
                QueueInvalidate();
            });

        var sub2 = hex.TerrainsChanged
            .ObserveOn(_scheduler)
            .Subscribe(_ =>
            {
                _terrainLabelCache.Remove(hex.Coordinates);
                QueueInvalidate();
            });

        return new CompositeDisposable(sub1, sub2);
    }

    private void QueueInvalidate()
    {
        lock (_syncLock)
        {
            if (_invalidateQueued) return;
            _invalidateQueued = true;
        }

        Dispatcher.UIThread.Post(() =>
        {
            lock (_syncLock) { _invalidateQueued = false; }
            InvalidateVisual();
        }, DispatcherPriority.Render);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        foreach (var tcs in _renderTcs) tcs.TrySetCanceled();
        _renderTcs.Clear();
        foreach (var sub in _subscriptions.Values)
            sub.Dispose();
        _subscriptions.Clear();
        _bitmapCache.Clear();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext context)
    {
        var config = _configuration;
        const double hexW = HexCoordinatesPixelExtensions.HexWidth;
        const double hexH = HexCoordinatesPixelExtensions.HexHeight;

        var allDirections = HexDirectionExtensions.AllDirections;

        // Sort hexes top-to-bottom, left-to-right for correct back-to-front overlap
        var sortedCoords = _sortedCoords;

        // Pass 1: all hex content layers
        foreach (var coords in sortedCoords)
        {
            var (hex, edges, water, road) = _hexData[coords];
            var ox = coords.H;
            var oy = coords.V;

            using (context.PushTransform(Matrix.CreateTranslation(ox, oy)))
            {
                // 1. Base biome
                var baseKey = DecodedBitmapCache.BaseKey(hex.Biome);
                var baseBitmap = _bitmapCache.GetOrSchedule(baseKey,
                    () => _bitmapCache.AssetService.GetBaseBiomeImage(hex.Biome));
                if (baseBitmap != null)
                    context.DrawImage(baseBitmap, new Rect(0, 0, hexW, hexH));

                // 2. Elevation edges
                foreach (var edge in edges)
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
                if (water != null)
                {
                    var waterKey = DecodedBitmapCache.WaterKey(hex.Biome,
                        water.CanonicalMask, water.RotationSteps);
                    var waterBitmap = _bitmapCache.GetOrSchedule(waterKey,
                        () => _bitmapCache.AssetService.GetWaterTextureImage(hex.Biome, water));
                    if (waterBitmap != null)
                        DrawRotatedBitmap(context, waterBitmap, hexW, hexH,
                            -water.RotationSteps * 60.0);
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
                if (!hex.HasTerrain(MakaMekTerrains.Rubble) && road != null)
                {
                    var roadKey = DecodedBitmapCache.RoadKey(hex.Biome,
                        road.CanonicalMask, road.RotationSteps);
                    var roadBitmap = _bitmapCache.GetOrSchedule(roadKey,
                        () => _bitmapCache.AssetService.GetRoadTextureImage(hex.Biome, road));
                    if (roadBitmap != null)
                        DrawRotatedBitmap(context, roadBitmap, hexW, hexH,
                            -road.RotationSteps * 60.0);
                }

                // 6. Hex polygon outline
                if (config.ShowOutline)
                    context.DrawGeometry(null, _whiteOutlinePen, _hexPolygon);

                // 7. Highlight fills/strokes ordered by RenderOrder
                if (!_sortedHighlightsCache.TryGetValue(coords, out var highlights))
                {
                    highlights = hex.Highlights.OrderBy(h => h.RenderOrder).ToList();
                    _sortedHighlightsCache[coords] = highlights;
                }
                foreach (var highlight in highlights)
                {
                    var (pen, fill) = GetHighlightPenAndFill(highlight);
                    if (fill != null || pen != null)
                        context.DrawGeometry(fill, pen, _hexPolygon);
                }

                // 8a. Coordinate label (top-center)
                if (config.ShowLabels)
                {
                    if (!_coordLabelCache.TryGetValue(coords, out var coordText))
                    {
                        coordText = CreateCoordLabel(coords);
                        _coordLabelCache[coords] = coordText;
                    }
                    var coordX = (hexW - coordText.Width) / 2;
                    context.DrawText(coordText, new Point(coordX, 2));
                }

                // 8b. Terrain info label (bottom-center)
                if (config.ShowLabels)
                {
                    if (!_terrainLabelCache.TryGetValue(coords, out var infoText))
                    {
                        var labelContent = GenerateLabelContent(hex);
                        infoText = labelContent != null
                            ? new FormattedText(labelContent, CultureInfo.CurrentCulture,
                                FlowDirection.LeftToRight, Typeface.Default, 11, _whiteHighlightBrush)
                            : null;
                        _terrainLabelCache[coords] = infoText;
                    }
                    if (infoText != null)
                    {
                        var infoX = (hexW - infoText.Width) / 2;
                        var infoY = hexH - infoText.Height - 2;
                        context.DrawText(infoText, new Point(infoX, infoY));
                    }
                }

                // 8c. Highlight text (center)
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
                        _whiteHighlightBrush)
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

        // Pass 2: boundary outlines on top of all hex content
        foreach (var coords in sortedCoords)
        {
            if (!_boundaryOutlines.TryGetValue(coords, out var boundary) || boundary.EdgeMask == 0)
                continue;

            var ox = coords.H;
            var oy = coords.V;

            using (context.PushTransform(Matrix.CreateTranslation(ox, oy)))
            {
                var bp = _boundaryPenCache[(boundary.Color, boundary.Thickness)];
                for (var i = 0; i < allDirections.Length; i++)
                {
                    if ((boundary.EdgeMask & (1 << i)) == 0) continue;
                    var (startIdx, endIdx) = allDirections[i].GetHexPointEdgeCornerIndices();
                    context.DrawLine(bp, _cornerPoints[startIdx], _cornerPoints[endIdx]);
                }
            }
        }

        foreach (var tcs in _renderTcs) tcs.TrySetResult();
        _renderTcs.Clear();
    }

    private static void DrawRotatedBitmap(DrawingContext context, Bitmap bitmap,
        double hexW, double hexH, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var cx = hexW / 2.0;
        var cy = hexH / 2.0;
        // Rotate around hex centre, matching old Image.RenderTransformOrigin=Center
        using (context.PushTransform(Matrix.CreateTranslation(-cx, -cy) *
                                     Matrix.CreateRotation(radians) *
                                     Matrix.CreateTranslation(cx, cy)))
        {
            context.DrawImage(bitmap, new Rect(0, 0, hexW, hexH));
        }
    }

    private (Pen? Pen, IBrush? Fill) GetHighlightPenAndFill(IHexHighlightType highlight)
    {
        return highlight switch
        {
            MovementReachableHighlight => _movementHighlight,
            AttackReachableHighlight => _attackHighlight,
            LosBlockingHighlight => _losBlockingHighlight,
            _ => (_whiteHighlightPen, null)
        };
    }

    private static IBrush ParseBrush(string colorHex)
    {
        if (Color.TryParse(colorHex, out var color))
            return new SolidColorBrush(color);
        return Brushes.White;
    }

    private static IBrush FindBrush(IAvaloniaResourcesLocator? locator, string key, IBrush fallback)
    {
        return locator?.TryFindResource(key) as IBrush ?? fallback;
    }

    private FormattedText CreateCoordLabel(HexCoordinates coords)
    {
        return new FormattedText(
            coords.ToString(),
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            12,
            _whiteHighlightBrush);
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

    /// <summary>
    /// Triggers a render pass and returns a task that completes after the next Render() call.
    /// Used to kick off bitmap loads before capturing.
    /// </summary>
    public Task WaitForNextRender()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _renderTcs.Add(tcs);
        InvalidateVisual();
        return tcs.Task;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var extent = GetMapExtentSize();
        return extent is { Width: > 0, Height: > 0 } 
            ? extent 
            : new Size(1, 1);
    }

    /// <summary>
    /// Computes the bounding size of the map in pixels based on hex coordinate extents,
    /// including standard padding. Returns default if no hex data is loaded.
    /// </summary>
    // ── Internal test helpers ────────────────────────────────────────────────
    internal IReadOnlyDictionary<HexCoordinates, HexRenderData> HexData => _hexData;
    internal IReadOnlyList<HexCoordinates> SortedCoords => _sortedCoords;
    internal IReadOnlyDictionary<HexCoordinates, IDisposable> Subscriptions => _subscriptions;

    public Size GetMapExtentSize()
    {
        if (_sortedCoords.Count == 0) return default;
        var maxH = _sortedCoords.Max(c => c.H);
        var maxV = _sortedCoords.Max(c => c.V);
        return new Size(
            maxH + 2 * HexCoordinatesPixelExtensions.HexWidth,
            maxV + 3 * HexCoordinatesPixelExtensions.HexHeight);
    }

    /// <summary>Compares <see cref="HexCoordinates"/> by (V, H) for sorted-coord binary-search inserts.</summary>
    private sealed class HexRenderOrderComparer : IComparer<HexCoordinates>
    {
        public static readonly HexRenderOrderComparer Instance = new();
        public int Compare(HexCoordinates x, HexCoordinates y)
        {
            var vCmp = x.V.CompareTo(y.V);
            return vCmp != 0 ? vCmp : x.H.CompareTo(y.H);
        }
    }
}
