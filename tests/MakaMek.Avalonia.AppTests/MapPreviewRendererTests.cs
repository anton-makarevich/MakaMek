using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Sanet.MakaMek.Avalonia.Controls.Services;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Services;
using Shouldly;

namespace MakaMek.Avalonia.AppTests;

/// <summary>
/// The preview must apply the same road-level rule as the map renderer: a road or bridge
/// neighbour two or more levels away is not connected. These tests drive the concrete
/// <see cref="SkiaMapPreviewRenderer"/> end to end and inspect the rendered pixels, so a
/// regression to raw masks (which would draw the connection regardless of level) fails here.
///
/// This lives in the AppTests assembly because it is the one configured with real Skia drawing;
/// MakaMek.Avalonia.Tests uses no-op headless drawing, where the produced bitmap is a 1x1
/// placeholder with no pixels to inspect.
/// </summary>
public class MapPreviewRendererTests
{
    private const int PreviewWidth = 300;

    [Fact]
    public Task GeneratePreview_DoesNotConnectBridgeTwoLevelsAboveRoad() => HarnessSession.Run(async () =>
    {
        // baseline: the neighbour exists but carries no road/bridge, so no connection can be drawn
        var baseline = await RenderPixels(CreateMap(neighbourTerrain: null));

        // rejected: the bridge surface is two levels above the road surface
        var rejected = await RenderPixels(CreateMap(new BridgeTerrain(2, 0)));

        // accepted: the bridge surface is one level above the road surface
        var accepted = await RenderPixels(CreateMap(new BridgeTerrain(1, 0)));

        // The rejected neighbour must render exactly like a neighbour with no road at all.
        rejected.ShouldBe(baseline);
        // Sanity check: the accepted neighbour does draw a connecting line.
        accepted.ShouldNotBe(baseline);
    });

    private static BattleMap CreateMap(Terrain? neighbourTerrain)
    {
        var map = new BattleMap(3, 3);

        var road = new Hex(new HexCoordinates(2, 2));
        road.AddTerrain(new RoadTerrain());
        map.AddHex(road);

        var neighbour = new Hex(new HexCoordinates(2, 1));
        if (neighbourTerrain != null)
            neighbour.AddTerrain(neighbourTerrain);
        map.AddHex(neighbour);

        return map;
    }

    private static async Task<byte[]> RenderPixels(BattleMap map)
    {
        var renderer = new SkiaMapPreviewRenderer(new TerrainBitmaskService());
        var result = await renderer.GeneratePreview(map, PreviewWidth);
        var bitmap = result.ShouldBeOfType<Bitmap>();

        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(
                new PixelRect(0, 0, size.Width, size.Height),
                handle.AddrOfPinnedObject(),
                pixels.Length,
                stride);
        }
        finally
        {
            handle.Free();
        }

        return pixels;
    }
}
