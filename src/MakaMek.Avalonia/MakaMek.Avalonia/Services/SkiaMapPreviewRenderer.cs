using System;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services;
using SkiaSharp;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sanet.MakaMek.Presentation.Models.Map;

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
            const double hexWidth = HexCoordinatesPresentationExtensions.HexWidth;
            const double hexHeight = HexCoordinatesPresentationExtensions.HexHeight;

            // Map bounds in hex units
            var mapUnitWidth = width * 0.75f;
            var mapUnitHeight = height + 0.5f;

            // Fit-to-width scale
            var scale = previewWidth / (mapUnitWidth * hexWidth);
            var previewHeight = Math.Max(1, (int)(mapUnitHeight * hexHeight * scale));

            // Dot diameter
            var dotDiameter = (float)(HexCoordinatesPresentationExtensions.HexWidth * scale * 0.95);

            var imageInfo = new SKImageInfo(previewWidth, previewHeight);
            using var surface = SKSurface.Create(imageInfo);
            if (surface == null) return null;
            var canvas = surface.Canvas;
            canvas.Clear(BackgroundColor);

            using var paint = new SKPaint();
            paint.IsAntialias = true;
            paint.Style = SKPaintStyle.Fill;

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
}

