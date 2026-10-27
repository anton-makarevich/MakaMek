namespace MakaMek.Tools.MW5Extractor.Reporting;

/// <summary>Result of <c>inspect</c>: what is actually readable inside one cooked asset.</summary>
public sealed record InspectReport
{
    public required string Path { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<ExportInfo> Exports { get; init; }
    public MeshInfo? Mesh { get; init; }
    public SkeletonInfo? Skeleton { get; init; }
    public AnimationInfo? Animation { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed record ExportInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }

    /// <summary>The CLR type CUE4Parse deserialised the export into; <c>UObject</c> means "found but not parsed".</summary>
    public required string ParsedType { get; init; }

    /// <summary>Deserialised tag count; <c>0</c> for MW5's custom cooked classes.</summary>
    public required int PropertyCount { get; init; }

    /// <summary>Tagged properties as read generically, even for classes CUE4Parse has no type for.</summary>
    public required IReadOnlyList<PropertyInfo> Properties { get; init; }
}

public sealed record PropertyInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }

    /// <summary>Compact value summary; struct members are stored as untyped fallbacks and only show their kind here.</summary>
    public string? Value { get; init; }
}

public sealed record MeshInfo
{
    public required IReadOnlyList<LodInfo> Lods { get; init; }
    public required IReadOnlyList<string> Bones { get; init; }
    public required IReadOnlyList<SocketInfo> Sockets { get; init; }
    public required IReadOnlyList<MaterialSlotInfo> MaterialSlots { get; init; }
    public string? SkeletonAsset { get; init; }
    public BoundsInfo? Bounds { get; init; }
}

public sealed record LodInfo
{
    public required int Index { get; init; }
    public required int Vertices { get; init; }
    public required int Triangles { get; init; }
    public required int Sections { get; init; }
}

public sealed record SocketInfo
{
    public required string Name { get; init; }
    public required string Bone { get; init; }
    public required VectorInfo Translation { get; init; }
    public required RotatorInfo Rotation { get; init; }
    public required VectorInfo Scale { get; init; }
}

public sealed record MaterialSlotInfo
{
    public required string Slot { get; init; }
    public string? Material { get; init; }
}

public sealed record SkeletonInfo
{
    public required int BoneCount { get; init; }
    public required IReadOnlyList<string> Bones { get; init; }
    public required IReadOnlyList<SocketInfo> Sockets { get; init; }
}

public sealed record AnimationInfo
{
    public required double LengthSeconds { get; init; }
    public required int Frames { get; init; }
    public required int BoneTracks { get; init; }
    public string? SkeletonAsset { get; init; }
}

public sealed record BoundsInfo
{
    public required VectorInfo Origin { get; init; }
    public required VectorInfo Extent { get; init; }
    public required float SphereRadius { get; init; }
}

public sealed record VectorInfo
{
    public required float X { get; init; }
    public required float Y { get; init; }
    public required float Z { get; init; }

    public static VectorInfo From(CUE4Parse.UE4.Objects.Core.Math.FVector value) =>
        new() { X = value.X, Y = value.Y, Z = value.Z };
}

public sealed record RotatorInfo
{
    public required float Pitch { get; init; }
    public required float Yaw { get; init; }
    public required float Roll { get; init; }

    public static RotatorInfo From(CUE4Parse.UE4.Objects.Core.Math.FRotator value) =>
        new() { Pitch = value.Pitch, Yaw = value.Yaw, Roll = value.Roll };
}
