using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>
/// A unit's decoded sprite atlas: the frames per clip plus the cell geometry they were cut with.
/// The atlas bitmap is kept alive because the cropped frames reference it.
/// </summary>
public sealed record UnitSpriteSet(
    Bitmap Atlas,
    int CellWidth,
    int CellHeight,
    IReadOnlyDictionary<string, UnitSpriteClip> Clips);