namespace MakaMek.Tools.MW5Extractor.Reporting;

/// <summary>Result of <c>clips</c>: which canonical clips a chassis actually ships, and where they live.</summary>
public sealed record ClipInventoryReport
{
    public required string Chassis { get; init; }

    /// <summary>Variant the report was scoped to, when the caller passed one.</summary>
    public string? Variant { get; init; }

    public required IReadOnlyList<ClipEntry> Clips { get; init; }

    /// <summary>Canonical clips MW5 does not ship for this chassis, with the reason they must be authored.</summary>
    public required IReadOnlyList<AbsentClip> Absent { get; init; }

    /// <summary>Animation assets that do not map to any canonical clip (first person, limping, turns, …).</summary>
    public required IReadOnlyList<string> Ignored { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>One canonical clip and the MW5 asset that supplies it.</summary>
public sealed record ClipEntry
{
    /// <summary>Canonical name: <c>Idle</c>, <c>Walk</c>, <c>Run</c>, <c>Jump</c>, <c>Shutdown</c>, <c>Destroyed</c>.</summary>
    public required string Name { get; init; }

    /// <summary><c>available</c> when an asset is mapped, <c>missing</c> when the chassis ships none.</summary>
    public required string Status { get; init; }

    /// <summary>Virtual path without extension of the chosen animation asset.</summary>
    public string? AssetPath { get; init; }

    /// <summary>All assets that matched, best first; the first one is the chosen asset.</summary>
    public required IReadOnlyList<string> Candidates { get; init; }

    public required bool Loop { get; init; }
    public string? Hold { get; init; }

    /// <summary>Source keys in the sequence, including the duplicated closing frame of a loop.</summary>
    public int Frames { get; init; }

    /// <summary>Sampled frames actually written to the model (the loop's duplicate last frame is dropped).</summary>
    public int SampledFrames { get; init; }

    /// <summary>Source sampling rate, derived from the key count and the sequence length.</summary>
    public float Fps { get; init; }

    public float DurationSeconds { get; init; }

    /// <summary>Bones in the source animation's skeleton that carry keys.</summary>
    public int AnimatedBones { get; init; }

    /// <summary>Horizontal travel of the skeleton's root bone, in metres. Must be ~0 for a board-anchored sprite.</summary>
    public float RootMotionMeters { get; init; }

    /// <summary>
    /// Horizontal displacement between the root bone's first and last key, in metres, measured before root
    /// motion is stripped. This is the stride the clip was authored with; <see cref="RootMotionMeters"/>
    /// only records the largest excursion, which includes any lateral sway.
    /// </summary>
    public float RootEndpointDisplacementMeters { get; init; }

    /// <summary>X component (glTF space) of the root displacement vector, in metres.</summary>
    public float RootDisplacementXMeters { get; init; }

    /// <summary>Z component (glTF space) of the root displacement vector, in metres.</summary>
    public float RootDisplacementZMeters { get; init; }

    /// <summary>
    /// True when the root advances monotonically along <see cref="RootEndpointDisplacementMeters"/>:
    /// no key moves backwards against the clip's forward direction.
    /// </summary>
    public bool RootProgressMonotonic { get; init; }

    /// <summary>Largest distance of any root key from the first-to-last displacement line, in metres.</summary>
    public float RootMaxLateralDeviationMeters { get; init; }

    /// <summary>Accumulated horizontal distance travelled by the root across all keys, in metres.</summary>
    public float RootPathLengthMeters { get; init; }

    /// <summary>Per-bone pose error between the first and last key of a loop, in degrees.</summary>
    public float LoopClosureDegrees { get; init; }

    /// <summary>Per-bone translation error between the first and last key of a loop, in metres.</summary>
    public float LoopClosureMeters { get; init; }

    public string? Skeleton { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed record AbsentClip
{
    public required string Name { get; init; }
    public required string Reason { get; init; }
}

/// <summary>A decoded clip ready to be written into a glTF animation: per-frame local transforms per bone.</summary>
public sealed record ClipSample
{
    public required ClipEntry Entry { get; init; }

    /// <summary>Bone name → sampled local transforms, already in glTF space (Y-up, metres).</summary>
    public required IReadOnlyDictionary<string, SampledBone> Bones { get; init; }
}

/// <summary>Per-frame local transform of one bone. A <c>null</c> channel means the source has no keys for it.</summary>
public sealed record SampledBone
{
    public required IReadOnlyList<System.Numerics.Quaternion> Rotations { get; init; }
    public IReadOnlyList<System.Numerics.Vector3>? Translations { get; init; }
    public IReadOnlyList<System.Numerics.Vector3>? Scales { get; init; }
}
