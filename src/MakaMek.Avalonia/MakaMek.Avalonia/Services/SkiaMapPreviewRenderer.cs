using Avalonia.Media.Imaging;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Services;
using SkiaSharp;
using System;
using System.Linq;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// Skia-based implementation of map preview renderer
/// </summary>
public class SkiaMapPreviewRenderer : IMapPreviewRenderer
{
    private const int DefaultDotDiameter = 4;
    
    // Colors from Colors.axaml
    private static readonly SKColor ClearTerrainColor = new(0x8F, 0xA5, 0x57); // PrimaryLightColor - light green
    private static readonly SKColor LightWoodsColor = new(0x6B, 0x8E, 0x23);   // PrimaryColor - medium green
    private static readonly SKColor HeavyWoodsColor = new(0x55, 0x6B, 0x2F);   // PrimaryDarkColor - dark green
    private static readonly SKColor BackgroundColor = new(0xE0, 0xE0, 0xE0);   // BackgroundColor

    /// <summary>
    /// Generates a preview image for the provided battle map by drawing dots at hex centers.
    /// </summary>
    public object? GeneratePreview(
        BattleMap map,
        int previewWidth = 300,
        int previewHeight = 300)
    {
        // Create a Skia surface to draw on
        var imageInfo = new SKImageInfo(previewWidth, previewHeight);
        using var surface = SKSurface.Create(imageInfo);
        var canvas = surface.Canvas;

        // Clear background
        canvas.Clear(BackgroundColor);

        // Calculate scaling to fit the map in the preview area
        var width = map.Width;
        var height = map.Height;
        var hexWidth = HexCoordinates.HexWidth;
        var hexHeight = HexCoordinates.HexHeight;
        var hexHorizontalSpacing = HexCoordinates.HexWidth * 0.75;
        
        // Calculate map bounds
        var mapPixelWidth = (width - 1) * hexHorizontalSpacing + hexWidth;
        var mapPixelHeight = height * hexHeight;
        
        // Calculate scale to fit preview with some padding
        var padding = 10;
        var scaleX = (previewWidth - 2 * padding) / mapPixelWidth;
        var scaleY = (previewHeight - 2 * padding) / mapPixelHeight;
        var scale = Math.Min(scaleX, scaleY);
        
        // Calculate dot diameter based on scale
        var dotDiameter = (float)(DefaultDotDiameter * scale);
        if (dotDiameter < 1) dotDiameter = 1;
        
        // Calculate offset to center the map
        var scaledMapWidth = mapPixelWidth * scale;
        var scaledMapHeight = mapPixelHeight * scale;
        var offsetX = (previewWidth - scaledMapWidth) / 2;
        var offsetY = (previewHeight - scaledMapHeight) / 2;

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
                var x = (float)(coordinates.H * scale + offsetX);
                var y = (float)(coordinates.V * scale + offsetY);
                
                // Draw dot
                using var paint = new SKPaint
                {
                    Color = terrainColor,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                
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

