using System.Text.Json.Serialization;

namespace Sanet.MakaMek.Assets.Models.Units;

/// <summary>
/// Metadata for the optional sprite atlas shipped inside an MMUX v2 package
/// (<c>unit-sprites.json</c>). The atlas holds one row per animation clip; every cell is
/// <see cref="CellWidth"/> × <see cref="CellHeight"/> with <see cref="Padding"/> pixels between cells.
/// </summary>
public sealed record UnitSpriteSheet
{
    /// <summary>Width of one animation cell in atlas pixels (1×; the atlas is not supersampled).</summary>
    public int CellWidth { get; init; }

    /// <summary>Height of one animation cell in atlas pixels.</summary>
    public int CellHeight { get; init; }

    /// <summary>Gutter between cells and around the atlas edge, in pixels.</summary>
    public int Padding { get; init; }

    /// <summary>Normalised pivot inside a cell: 0.5, 0.5 is the centre.</summary>
    public double[] Pivot { get; init; } = [0.5, 0.5];

    /// <summary>Unit of <see cref="UnitSpriteAnimation.DistancePerCycle"/>; currently <c>spritePixels</c>.</summary>
    public string? DistanceUnits { get; init; }

    /// <summary>Atlas row and timing per clip name (Walk, Run, …).</summary>
    public Dictionary<string, UnitSpriteAnimation> Animations { get; init; } = [];

    /// <summary>
    /// True when the sheet carries enough information to render at least one frame.
    /// </summary>
    [JsonIgnore]
    public bool IsValid =>
        CellWidth > 0 &&
        CellHeight > 0 &&
        Padding >= 0 &&
        Animations.Count > 0 &&
        Animations.Values.Any(animation => animation.Frames > 0);
}

/// <summary>One clip stored in an MMUX v2 sprite atlas.</summary>
public sealed record UnitSpriteAnimation
{
    /// <summary>Zero-based atlas row holding this clip's frames.</summary>
    public int Row { get; init; }

    /// <summary>Number of frames in the row, left to right.</summary>
    public int Frames { get; init; }

    /// <summary>Authoring playback rate; informational, frame phase is distance-driven at runtime.</summary>
    public double Fps { get; init; }

    /// <summary>Whether the clip wraps. Only looping clips are played at runtime today.</summary>
    public bool Loop { get; init; }

    /// <summary>
    /// Distance the unit travels during one full animation cycle, in sprite pixels. Dividing the
    /// travelled distance by this value and multiplying by <see cref="Frames"/> gives the frame phase,
    /// which keeps the feet planted when path and frames share one clock.
    /// </summary>
    public double DistancePerCycle { get; init; }
}

/// <summary>Atlas bytes plus their parsed metadata, as stored in the unit cache.</summary>
public sealed record UnitAnimationData(byte[] Atlas, UnitSpriteSheet Sheet);
