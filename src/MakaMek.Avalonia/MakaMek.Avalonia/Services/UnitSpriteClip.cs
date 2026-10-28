using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace Sanet.MakaMek.Avalonia.Services;

/// <summary>One animation clip decoded from an MMUX v2 sprite atlas, ready for frame swapping.</summary>
/// <param name="Name">Clip name (Walk, Run).</param>
/// <param name="Frames">Pre-cropped frames in playback order.</param>
/// <param name="DistancePerCycle">Distance travelled during one full cycle, in sprite pixels.</param>
/// <param name="Loop">Whether the clip wraps.</param>
/// <param name="Fps">Authoring playback rate; the runtime drives frames from distance.</param>
public sealed record UnitSpriteClip(
    string Name,
    IReadOnlyList<CroppedBitmap> Frames,
    double DistancePerCycle,
    bool Loop,
    double Fps);