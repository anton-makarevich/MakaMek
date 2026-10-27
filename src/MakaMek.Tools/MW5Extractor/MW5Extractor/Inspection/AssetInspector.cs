using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Assets.Exports.Engine;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Objects;
using CUE4Parse.UE4.Assets.Objects.Properties;
using CUE4Parse.UE4.Objects.UObject;
using MakaMek.Tools.MW5Extractor.Reporting;

namespace MakaMek.Tools.MW5Extractor.Inspection;

/// <summary>Reads what CUE4Parse can actually parse out of a single cooked asset.</summary>
public static class AssetInspector
{
    public static InspectReport Inspect(UnrealArchive archive, string assetPath)
    {
        var file = archive.FindFile(assetPath) ?? throw new AssetNotFoundException(assetPath);
        var exports = archive.LoadPackage(file.PathWithoutExtension).GetExports().ToList();
        var warnings = new List<string>();

        var exportInfos = exports
            .Select(export => new ExportInfo
            {
                Name = export.Name,
                Type = export.ExportType,
                ParsedType = export.GetType().Name,
                PropertyCount = export.Properties.Count,
                Properties = export.Properties
                    .OfType<FPropertyTag>()
                    .Select(tag => new PropertyInfo
                    {
                        Name = tag.Name.Text,
                        Type = tag.PropertyType.Text,
                        Value = Summarize(tag.Tag)
                    })
                    .ToList()
            })
            .ToList();

        var mesh = exports.OfType<USkinnedAsset>().Select(InspectMesh).FirstOrDefault();
        var skeleton = exports.OfType<USkeleton>().Select(InspectSkeleton).FirstOrDefault();
        var animation = exports.OfType<UAnimSequence>().Select(InspectAnimation).FirstOrDefault();

        if (mesh is null && skeleton is null && animation is null)
        {
            var hasProperties = exportInfos.Any(export => export.PropertyCount > 0);
            warnings.Add(hasProperties
                ? "No mesh, skeleton or animation export. This is one of MW5's custom cooked classes: CUE4Parse knows its "
                  + "class name and generic tags; `extract --format json` dumps the full tagged structure behind them."
                : "No mesh, skeleton or animation export and no readable tags; inspect the export list above.");
        }

        if (mesh is { Sockets.Count: 0 })
        {
            warnings.Add("The mesh defines no sockets. Weapon/hardpoint mount points are not stored on the body mesh.");
        }

        return new InspectReport
        {
            Path = file.Path,
            Name = file.NameWithoutExtension,
            Exports = exportInfos,
            Mesh = mesh,
            Skeleton = skeleton,
            Animation = animation,
            Warnings = warnings
        };
    }

    private static string? Summarize(FPropertyTagType? tag)
    {
        var text = tag?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        const int limit = 300;
        text = text.ReplaceLineEndings(" ");
        return text.Length <= limit ? text : text[..limit] + "…";
    }

    private static MeshInfo InspectMesh(USkinnedAsset mesh)
    {
        var lods = (mesh.LODModels ?? [])
            .Select((lod, index) => new LodInfo
            {
                Index = index,
                Vertices = lod.NumVertices,
                Triangles = lod.Sections?.Sum(section => section.NumTriangles) ?? 0,
                Sections = lod.Sections?.Length ?? 0
            })
            .ToList();

        var bounds = mesh.Bounds;

        return new MeshInfo
        {
            Lods = lods,
            Bones = (mesh.ReferenceSkeleton?.FinalRefBoneInfo ?? [])
                .Select(bone => bone.Name.Text)
                .ToList(),
            Sockets = ReadSockets(mesh.Sockets),
            MaterialSlots = (mesh.SkeletalMaterials ?? [])
                .Select(material => new MaterialSlotInfo
                {
                    Slot = material.MaterialSlotName.Text,
                    Material = material.MaterialInterface?.Name
                })
                .ToList(),
            SkeletonAsset = mesh.Skeleton?.Name,
            Bounds = bounds is null
                ? null
                : new BoundsInfo
                {
                    Origin = VectorInfo.From(bounds.Origin),
                    Extent = VectorInfo.From(bounds.BoxExtent),
                    SphereRadius = bounds.SphereRadius
                }
        };
    }

    private static SkeletonInfo InspectSkeleton(USkeleton skeleton)
    {
        return new SkeletonInfo
        {
            BoneCount = skeleton.BoneCount,
            Bones = (skeleton.ReferenceSkeleton?.FinalRefBoneInfo ?? [])
                .Select(bone => bone.Name.Text)
                .ToList(),
            Sockets = ReadSockets(skeleton.Sockets)
        };
    }

    private static AnimationInfo InspectAnimation(UAnimSequence animation)
    {
        return new AnimationInfo
        {
            LengthSeconds = animation.SequenceLength,
            Frames = animation.NumFrames,
            BoneTracks = animation.CompressedTrackToSkeletonMapTable?.Length
                         ?? animation.TrackToSkeletonMapTable?.Length
                         ?? 0,
            SkeletonAsset = animation.Skeleton?.Name
        };
    }

    private static List<SocketInfo> ReadSockets(IEnumerable<FPackageIndex>? sockets)    {
        return (sockets ?? [])
            .Select(index => index.Load() as USkeletalMeshSocket)
            .Where(socket => socket is not null)
            .Select(socket => new SocketInfo
            {
                Name = socket!.SocketName.Text,
                Bone = socket.BoneName.Text,
                Translation = VectorInfo.From(socket.RelativeLocation),
                Rotation = RotatorInfo.From(socket.RelativeRotation),
                Scale = VectorInfo.From(socket.RelativeScale)
            })
            .ToList();
    }
}
