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

    public required IReadOnlyList<AssemblyPart> Parts { get; init; }

    /// <summary>Loadout entries that could not be placed, with the reason. Never silently dropped.</summary>
    public required IReadOnlyList<string> Unmapped { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
    public required long ElapsedMilliseconds { get; init; }

    public static VectorInfo Vector(Vector3 value) => new() { X = value.X, Y = value.Y, Z = value.Z };
}
