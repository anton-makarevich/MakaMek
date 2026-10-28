using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using Sanet.MakaMek.Assets.Models.Units;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Services;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// Reads sprite atlases from <see cref="IUnitCachingService"/> and cuts them into playable frames.
/// </summary>
/// <remarks>
/// Decoding and cropping happen once per model. Frames are <see cref="CroppedBitmap"/>s over a single
/// decoded atlas, so swapping frames never re-decodes the PNG. Frames are drawn 1:1 at the cell size,
/// which makes the atlas's <c>distancePerCycle</c> values (in sprite pixels) directly comparable with
/// the board's pixel distances.
/// </remarks>
public class UnitSpriteService : IUnitSpriteService<UnitSpriteSet>
{
    private readonly IUnitCachingService _unitCachingService;
    private readonly ILogger<UnitSpriteService> _logger;
    private readonly ConcurrentDictionary<string, UnitSpriteSet?> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public UnitSpriteService(IUnitCachingService unitCachingService, ILogger<UnitSpriteService> logger)
    {
        _unitCachingService = unitCachingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UnitSpriteSet?> GetSpriteSet(string model)
    {
        if (_cache.TryGetValue(model, out var cached)) return cached;

        UnitAnimationData? animation;
        try
        {
            animation = await _unitCachingService.GetUnitAnimation(model);
        }
        catch (Exception ex)
        {
            // The unit package cache is shared with the static image path, which handles its own
            // failures; a sprite lookup must never stop the unit from rendering at all.
            _logger.LogError(ex, "Error reading the sprite data of unit '{Model}'", model);
            return null;
        }

        var decoded = animation is null ? null : Decode(model, animation);
        _cache[model] = decoded;
        return decoded;
    }

    /// <summary>
    /// Drops every decoded atlas. Called when the board is rebuilt so a provider change (for example a
    /// newly configured local folder with v2 packages) is picked up.
    /// </summary>
    public void ClearCache()
    {
        foreach (var set in _cache.Values)
        {
            set?.Atlas.Dispose();
        }

        _cache.Clear();
    }

    private UnitSpriteSet? Decode(string model, UnitAnimationData animation)
    {
        try
        {
            using var stream = new MemoryStream(animation.Atlas);
            var atlas = new Bitmap(stream);
            var sheet = animation.Sheet;

            var clips = new Dictionary<string, UnitSpriteClip>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, clip) in sheet.Animations)
            {
                if (clip.Frames <= 0 || clip.Row < 0) continue;

                var frames = new List<CroppedBitmap>(clip.Frames);
                for (var column = 0; column < clip.Frames; column++)
                {
                    var rect = GetFrameRect(sheet.Padding, sheet.CellWidth, sheet.CellHeight, clip.Row, column);

                    if (rect.Right > atlas.PixelSize.Width || rect.Bottom > atlas.PixelSize.Height)
                    {
                        _logger.LogWarning(
                            "Sprite clip '{Clip}' of unit '{Model}' points outside its atlas " +
                            "({AtlasWidth}x{AtlasHeight}, frame {FrameX},{FrameY} {FrameWidth}x{FrameHeight}); " +
                            "skipping the clip",
                            name, model, atlas.PixelSize.Width, atlas.PixelSize.Height,
                            rect.X, rect.Y, rect.Width, rect.Height);
                        frames.Clear();
                        break;
                    }

                    frames.Add(new CroppedBitmap(atlas, rect));
                }

                if (frames.Count == 0) continue;

                clips[name] = new UnitSpriteClip(
                    name,
                    frames,
                    clip.DistancePerCycle,
                    clip.Loop,
                    clip.Fps);
            }

            if (clips.Count != 0) return new UnitSpriteSet(atlas, sheet.CellWidth, sheet.CellHeight, clips);
            atlas.Dispose();
            return null;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decoding the sprite atlas of unit '{Model}'", model);
            return null;
        }
    }

    /// <summary>
    /// The atlas cell rectangle: one padding gutter at the start of the row/column, then
    /// <c>padding + cell + padding</c> per step. Kept public and pure so the crop contract is testable
    /// without decoding an image.
    /// </summary>
    public static PixelRect GetFrameRect(int padding, int cellWidth, int cellHeight, int row, int column) =>
        new(
            padding + column * (cellWidth + padding),
            padding + row * (cellHeight + padding),
            cellWidth,
            cellHeight);
}
