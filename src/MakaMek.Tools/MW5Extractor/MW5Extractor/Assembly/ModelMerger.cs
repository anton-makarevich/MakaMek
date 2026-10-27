using System.Numerics;
using MakaMek.Tools.MW5Extractor.Reporting;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;
using SharpGLTF.Scenes;

namespace MakaMek.Tools.MW5Extractor.Assembly;

/// <summary>One mesh to merge, already positioned in the chassis' space.</summary>
public sealed record MergePart
{
    public required string Label { get; init; }
    public required string GlbPath { get; init; }
    public required Matrix4x4 Transform { get; init; }
}

public sealed record MergeResult
{
    public required int VertexCount { get; init; }
    public required int TriangleCount { get; init; }
    public required IReadOnlyList<int> PartVertices { get; init; }
    public required IReadOnlyList<int> PartTriangles { get; init; }
    public required BoundsInfo Bounds { get; init; }
    public required float PlanViewExtentMeters { get; init; }
}

/// <summary>
/// Merges several glTF part exports into one static, single-material glTF, in MakaMek canonical space:
/// metres, Y-up, mech facing <b>+Z</b>, pivot at the footprint centre on the ground plane.
/// </summary>
/// <remarks>
/// The parts are baked at their rest pose (skinning at bind pose is the identity) and flattened to a static mesh:
/// stage 2 only needs the silhouette, and the board-look material replaces MW5 materials anyway.
/// </remarks>
public static class ModelMerger
{
    public static MergeResult Merge(IReadOnlyList<MergePart> parts, string outputPath)
    {
        var material = new MaterialBuilder("board")
            .WithDoubleSide(true)
            .WithMetallicRoughness(0.1f, 0.85f);

        var mesh = new MeshBuilder<VertexPositionNormal, VertexEmpty>("assembled");
        var primitive = mesh.UsePrimitive(material);

        var partVertices = new int[parts.Count];
        var partTriangles = new int[parts.Count];
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            var sourceModel = ModelRoot.Load(part.GlbPath);

            foreach (var sourceMesh in sourceModel.LogicalMeshes)
            {
                foreach (var sourcePrimitive in sourceMesh.Primitives)
                {
                    var positions = sourcePrimitive.GetVertexAccessor("POSITION")?.AsVector3Array();
                    var normals = sourcePrimitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
                    var indices = sourcePrimitive.GetIndices();
                    if (positions is null || positions.Count == 0 || indices.Count < 3)
                    {
                        continue;
                    }

                    var vertices = new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>[positions.Count];
                    for (var vertex = 0; vertex < positions.Count; vertex++)
                    {
                        var position = Vector3.Transform(positions[vertex], part.Transform);
                        var normal = normals is not null && vertex < normals.Count
                            ? SafeNormalize(Vector3.TransformNormal(normals[vertex], part.Transform))
                            : Vector3.UnitY;

                        vertices[vertex] = new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexEmpty>(
                            new VertexPositionNormal(position, normal));
                        min = Vector3.Min(min, position);
                        max = Vector3.Max(max, position);
                    }

                    for (var triangle = 0; triangle + 2 < indices.Count; triangle += 3)
                    {
                        primitive.AddTriangle(
                            vertices[indices[triangle]],
                            vertices[indices[triangle + 1]],
                            vertices[indices[triangle + 2]]);
                    }

                    partVertices[index] += positions.Count;
                    partTriangles[index] += indices.Count / 3;
                }
            }
        }

        if (partVertices.All(count => count == 0))
        {
            throw new InvalidOperationException("Nothing to merge: every part exported without geometry.");
        }

        // Canonical facing: the source faces +X, MakaMek models face +Z. Rotate about Y, then put the
        // footprint centre on the origin and the lowest point on the ground plane.
        var rotation = Matrix4x4.CreateRotationY(-MathF.PI / 2f);
        var rotatedMin = new Vector3(float.MaxValue);
        var rotatedMax = new Vector3(float.MinValue);
        foreach (var corner in Corners(min, max))
        {
            var rotated = Vector3.Transform(corner, rotation);
            rotatedMin = Vector3.Min(rotatedMin, rotated);
            rotatedMax = Vector3.Max(rotatedMax, rotated);
        }

        var pivot = new Vector3(
            (rotatedMin.X + rotatedMax.X) / 2f,
            rotatedMin.Y,
            (rotatedMin.Z + rotatedMax.Z) / 2f);
        var canonical = rotation * Matrix4x4.CreateTranslation(-pivot);

        var scene = new SceneBuilder();
        scene.AddRigidMesh(mesh, canonical);

        var model = scene.ToGltf2();
        model.SaveGLB(outputPath);

        var size = rotatedMax - rotatedMin;
        var center = (rotatedMax + rotatedMin) / 2f - pivot;

        return new MergeResult
        {
            VertexCount = partVertices.Sum(),
            TriangleCount = partTriangles.Sum(),
            PartVertices = partVertices,
            PartTriangles = partTriangles,
            Bounds = new BoundsInfo
            {
                Origin = AssemblyReport.Vector(center),
                Extent = AssemblyReport.Vector(size / 2f),
                SphereRadius = size.Length() / 2f
            },
            PlanViewExtentMeters = Math.Max(size.X, size.Z)
        };
    }

    private static IEnumerable<Vector3> Corners(Vector3 min, Vector3 max)
    {
        yield return new Vector3(min.X, min.Y, min.Z);
        yield return new Vector3(min.X, min.Y, max.Z);
        yield return new Vector3(min.X, max.Y, min.Z);
        yield return new Vector3(min.X, max.Y, max.Z);
        yield return new Vector3(max.X, min.Y, min.Z);
        yield return new Vector3(max.X, min.Y, max.Z);
        yield return new Vector3(max.X, max.Y, min.Z);
        yield return new Vector3(max.X, max.Y, max.Z);
    }

    private static Vector3 SafeNormalize(Vector3 value) =>
        value.LengthSquared() < 1e-8f ? Vector3.UnitY : Vector3.Normalize(value);
}
