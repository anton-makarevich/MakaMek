using System.Numerics;
using MakaMek.Tools.MW5Extractor.Reporting;

namespace MakaMek.Tools.MW5Extractor.Assembly;

/// <summary>Assemble one variant: chassis body + cockpit + glass + the weapons of its loadout.</summary>
public sealed record AssemblyRequest
{
    /// <summary>Chassis folder or variant id, e.g. <c>Atlas</c> or <c>AS7-D</c>.</summary>
    public required string Chassis { get; init; }

    /// <summary>Variant to assemble; defaults to the first variant that has a loadout.</summary>
    public string? Variant { get; init; }

    public required string OutputDirectory { get; init; }

    public bool SkipCockpit { get; init; }
    public bool SkipWeapons { get; init; }

    /// <summary>
    /// Canonical clips to bake into a skinned, animated model (<c>Walk</c>, <c>Run</c>, …). Empty keeps the static
    /// rest-pose merge.
    /// </summary>
    public IReadOnlyList<string> Clips { get; init; } = [];

    /// <summary>Optional path to a clip vocabulary; defaults to the one shipped with the tool.</summary>
    public string? ClipMapPath { get; init; }
}

/// <summary>One mesh placed in the assembled model.</summary>
public sealed record AssemblyPart
{
    /// <summary><c>body</c>, <c>cockpit</c>, <c>glass</c> or <c>weapon</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Source asset (virtual path without extension).</summary>
    public required string AssetPath { get; init; }

    /// <summary>Hardpoint slot this part belongs to, when it is a weapon.</summary>
    public string? Slot { get; init; }

    /// <summary>Mech skeleton socket the part is attached to, when it is a weapon.</summary>
    public string? Socket { get; init; }

    /// <summary>Bone the attachment resolves to (sockets are identity offsets in MW5).</summary>
    public string? Bone { get; init; }

    /// <summary>MW5 weapon tag that selected the visual, e.g. <c>Weapon.Ballistic.AutoCannon.Standard.AC20</c>.</summary>
    public string? WeaponTag { get; init; }

    /// <summary>Attachment translation in the assembled model, in metres.</summary>
    public required VectorInfo Translation { get; init; }

    public string? GlbPath { get; init; }
    public int VertexCount { get; init; }
    public int TriangleCount { get; init; }
}

/// <summary>Result of <c>assemble</c>: one renderable model plus the mapping that produced it.</summary>
public sealed record AssemblyReport
{
    public required string Chassis { get; init; }
    public required string Variant { get; init; }
    public required string ModelPath { get; init; }
    public required int VertexCount { get; init; }
    public required int TriangleCount { get; init; }
    public required BoundsInfo Bounds { get; init; }

    /// <summary>Largest plan-view span in metres — what the 90° camera sees.</summary>
    public required float PlanViewExtentMeters { get; init; }

    /// <summary>
    /// <c>canonical</c> (Y-up, facing +Z, footprint centred) for static models, or <c>mw5-source</c> (Y-up, facing
    /// +X) for animated ones, which have to stay in the skeleton's own space; render those with
    /// <c>--model-yaw -90</c>.
    /// </summary>
    public string ModelSpace { get; init; } = "canonical";

    /// <summary>Yaw the driver must apply to reach canonical plan-view orientation (0 or -90).</summary>
    public float ModelYawDegrees { get; init; }

    public required IReadOnlyList<AssemblyPart> Parts { get; init; }

    /// <summary>Clips baked into the model, with their source asset and sampling metadata. Empty for a static model.</summary>
    public IReadOnlyList<ClipEntry> Animations { get; init; } = [];

    /// <summary>Loadout entries that could not be placed, with the reason. Never silently dropped.</summary>
    public required IReadOnlyList<string> Unmapped { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
    public required long ElapsedMilliseconds { get; init; }

    public static VectorInfo Vector(Vector3 value) => new() { X = value.X, Y = value.Y, Z = value.Z };
}
