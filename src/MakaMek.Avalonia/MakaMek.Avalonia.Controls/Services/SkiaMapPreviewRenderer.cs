using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Services;
using Sanet.MakaMek.Map.Services;
using SkiaSharp;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Avalonia.Controls.Services;

public class SkiaMapPreviewRenderer : IMapPreviewRenderer
{
    private static readonly SKColor ClearTerrainColor = new(0x8F, 0xA5, 0x57);
    private static readonly SKColor LightWoodsColor = new(0x6B, 0x8E, 0x23);
    private static readonly SKColor HeavyWoodsColor = new(0x55, 0x6B, 0x2F);
    private static readonly SKColor RoughColor = new(0x70, 0x78, 0x72);
    private static readonly SKColor WaterColor = new(0x46, 0x82, 0xB4);
    private static readonly SKColor BackgroundColor = new(0xE0, 0xE0, 0xE0);
    private static readonly SKColor RoadColor = new(0x33, 0x33, 0x33);

    private static readonly (float Dx, float Dy)[] DirectionOffsets =
    [
        (0, -43.3f),
        (37.5f, -21.65f),
        (37.5f, 21.65f),
        (0, 43.3f),
        (-37.5f, 21.65f),
        (-37.5f, -21.65f)
    ];

    private readonly ITerrainBitmaskService _bitmaskService;

    public SkiaMapPreviewRenderer(ITerrainBitmaskService bitmaskService)
    {
        _bitmaskService = bitmaskService;
    }

    public async Task<object?> GeneratePreviewAsync(BattleMap map, int previewWidth = 300, CancellationToken cancellationToken = default)
    {
        if (previewWidth <= 0) throw new ArgumentOutOfRangeException(nameof(previewWidth));

        return await Task.Run<object?>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var width = map.Width;
            var height = map.Height;
            const double hexWidth = HexCoordinatesPixelExtensions.HexWidth;
            const double hexHeight = HexCoordinatesPixelExtensions.HexHeight;

            var mapUnitWidth = width * 0.75f;
            var mapUnitHeight = height + 0.5f;

            var scale = previewWidth / (mapUnitWidth * hexWidth);
            var previewHeight = Math.Max(1, (int)(mapUnitHeight * hexHeight * scale));

            var dotDiameter = (float)(HexCoordinatesPixelExtensions.HexWidth * scale * 0.95);

            var imageInfo = new SKImageInfo(previewWidth, previewHeight);
            using var surface = SKSurface.Create(imageInfo);
            if (surface == null) return null;
            var canvas = surface.Canvas;
            canvas.Clear(BackgroundColor);

            using var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Style = SKPaintStyle.Fill;

            using var strokePaint = new SKPaint();
            strokePaint.IsAntialias = true;
            strokePaint.Style = SKPaintStyle.Stroke;
            strokePaint.StrokeWidth = dotDiameter * 0.15f;

            using var roadPaint = new SKPaint();
            roadPaint.IsAntialias = true;
            roadPaint.Style = SKPaintStyle.Stroke;
            roadPaint.StrokeWidth = dotDiameter * 0.2f;
            roadPaint.Color = RoadColor;

            for (var q = 1; q <= width; q++)
            {
                for (var r = 1; r <= height; r++)
                {
                    if ((q * height + r) % 64 == 0 && cancellationToken.IsCancellationRequested)
                        return null;

                    var coordinates = new HexCoordinates(q, r);
                    var hex = map.GetHex(coordinates);
                    if (hex == null) continue;

                    paint.Color = GetTerrainColor(hex);

                    var x = (float)((coordinates.H - hexWidth * 0.35) * scale);
                    var y = (float)(coordinates.V * scale);
                    canvas.DrawCircle(x, y, dotDiameter / 2, paint);

                    if (hex.HasTerrain(MakaMekTerrains.Road) || hex.HasTerrain(MakaMekTerrains.Bridge))
                    {
                        var mask = (byte)(_bitmaskService.ComputeRawBitmask(map, coordinates, MakaMekTerrains.Road) 
                                          | _bitmaskService.ComputeRawBitmask(map, coordinates, MakaMekTerrains.Bridge));
                        for (var i = 0; i < 6; i++)
                        {
                            if ((mask & (1 << i)) == 0) continue;
                            var offset = DirectionOffsets[i];
                            canvas.DrawLine(x, y,
                                x + offset.Dx * (float)scale,
                                y + offset.Dy * (float)scale,
                                roadPaint);
                        }
                    }

                    if (hex.Level==0) continue;
                    
                    var ringColor = GetElevationRingColor(hex);
                    strokePaint.Color = ringColor;
                    canvas.DrawCircle(x, y, dotDiameter / 2 * 0.85f, strokePaint);
                }
            }

            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = data.AsStream();
            return new Bitmap(stream);
        }, cancellationToken);
    }

    private static SKColor GetTerrainColor(Hex hex)
    {
        var terrains = hex.GetTerrains().ToList();
        if (terrains.Count == 0)
            return ClearTerrainColor;

        var terrain = terrains.First();
        return terrain.Id switch
        {
            MakaMekTerrains.Clear => ClearTerrainColor,
            MakaMekTerrains.LightWoods => LightWoodsColor,
            MakaMekTerrains.HeavyWoods => HeavyWoodsColor,
            MakaMekTerrains.Rough => RoughColor,
            MakaMekTerrains.Water => WaterColor,
            _ => ClearTerrainColor
        };
    }

    private static SKColor GetElevationRingColor(Hex hex)
    {
        if (hex.Level > 0)
        {
            var darkValue = 0x77L - hex.Level * 0x22L;
            var dark = (byte)Math.Clamp(darkValue, 0x10L, 0x77L);
            return new SKColor(dark, dark, dark);
        }

        var absLevel = Math.Abs((long)hex.Level);
        var lightValue = 0x88L + absLevel * 0x22L;
        var light = (byte)Math.Clamp(lightValue, 0x88L, 0xFFL);
        return new SKColor(light, light, light, 0xDD);
    }
}
