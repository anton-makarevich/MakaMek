using System;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Services;
using SkiaSharp;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// Skia-based implementation of a map preview renderer
/// </summary>
public class SkiaMapPreviewRenderer : IMapPreviewRenderer
{
    // Colors from Colors.axaml
    private static readonly SKColor ClearTerrainColor = new(0x8F, 0xA5, 0x57); // PrimaryLightColor - light green
    private static readonly SKColor LightWoodsColor = new(0x6B, 0x8E, 0x23); // PrimaryColor - medium green
    private static readonly SKColor HeavyWoodsColor = new(0x55, 0x6B, 0x2F); // PrimaryDarkColor - dark green
    private static readonly SKColor BackgroundColor = new(0xE0, 0xE0, 0xE0); // BackgroundColor

    /// <summary>
    /// Generates a preview image for the provided battle map by drawing dots at hex centers.
    /// </summary>
    /// <param name="map">The battle map to generate a preview for</param>
    /// <param name="previewWidth">Width of the preview in pixels</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A bitmap containing the rendered map preview</returns>
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

            // Map bounds in hex units
            var mapUnitWidth = width * 0.75f;
            var mapUnitHeight = height + 0.5f;

            // Fit-to-width scale
            var scale = previewWidth / (mapUnitWidth * hexWidth);
            var previewHeight = Math.Max(1, (int)(mapUnitHeight * hexHeight * scale));

            // Dot diameter
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
            _ => ClearTerrainColor
        };
    }

    private static SKColor GetElevationRingColor(Hex hex)
    {
        if (hex.Level > 0)
        {
            // Darken from base dark toward near-black
            // level 1 -> 0x55, level 2 -> 0x33, level 3+ -> 0x11
            var darkValue = 0x77L - hex.Level * 0x22L;
            var dark = (byte)Math.Clamp(darkValue, 0x10L, 0x77L);
            return new SKColor(dark, dark, dark);
        }

        // Lighten from base light toward near-white
        // level -1 -> 0xAA, level -2 -> 0xCC, level -3+ -> 0xEE
        var absLevel = Math.Abs((long)hex.Level);
        var lightValue = 0x88L + absLevel * 0x22L;
        var light = (byte)Math.Clamp(lightValue, 0x88L, 0xFFL);
        return new SKColor(light, light, light, 0xDD); // Slightly transparent
    }
}

