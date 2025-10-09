using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services;
using SkiaSharp;
using System.Linq;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// Skia-based implementation of map preview renderer
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
    /// <returns>A bitmap containing the rendered map preview</returns>
    public object GeneratePreview(BattleMap map, int previewWidth = 300)
    {
        // Calculate scaling to fit the map in the preview area
        var width = map.Width;
        var height = map.Height;
        const double hexWidth = HexCoordinates.HexWidth;
        const double hexHeight = HexCoordinates.HexHeight;

        // Calculate map bounds in hex units
        var mapUnitWidth = width * 0.75f; // Convert hex width to units
        var mapUnitHeight = height + 0.5f; // Convert hex height to units
        
        // Calculate scale to fit preview
        var scale = previewWidth / (mapUnitWidth * hexWidth);
        var previewHeight = (int)(mapUnitHeight * hexHeight * scale);

        // Calculate dot diameter based on hex width and scale
        var dotDiameter = (float)(HexCoordinates.HexWidth * scale * 0.95);
        
        // Create a Skia surface to draw on
        var imageInfo = new SKImageInfo(previewWidth, previewHeight);
        using var surface = SKSurface.Create(imageInfo);
        var canvas = surface.Canvas;

        // Clear background
        canvas.Clear(BackgroundColor);
        
        // Draw hexes as dots based on map contents
        for (var q = 1; q <= width; q++)
        {
            for (var r = 1; r <= height; r++)
            {
                var coordinates = new HexCoordinates(q, r);
                var hex = map.GetHex(coordinates);
                if (hex == null) continue;

                // Get terrain color
                var terrainColor = GetTerrainColor(hex);

                // Calculate position
                var x = (float)((coordinates.H-hexWidth*0.35) * scale);
                var y = (float)(coordinates.V * scale);

                // Draw dot
                using var paint = new SKPaint();
                paint.Color = terrainColor;
                paint.IsAntialias = true;
                paint.Style = SKPaintStyle.Fill;

                canvas.DrawCircle(x, y, dotDiameter / 2, paint);
            }
        }

        // Create Avalonia bitmap from Skia image
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = data.AsStream();

        return new Bitmap(stream);
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

