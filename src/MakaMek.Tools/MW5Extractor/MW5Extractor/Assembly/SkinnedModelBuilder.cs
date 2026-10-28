using System.Numerics;
using MakaMek.Tools.MW5Extractor.Reporting;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Schema2;
using SharpGLTF.Scenes;

namespace MakaMek.Tools.MW5Extractor.Assembly;

/// <summary>One part of an animated model: the skinned body, or a rigid part attached to a bone.</summary>
public sealed record SkinnedPart
{
    public required string Kind { get; init; }
    public required string GlbPath { get; init; }

    /// <summary>Rest-pose transform of the part in the body's exported space (metres, Y-up).</summary>
    public required Matrix4x4 Transform { get; init; }

    /// <summary>Bone the part is attached to; <c>null</c> for the body itself.</summary>
    public string? Bone { get; init; }

    public bool IsBody => Bone is null;
}

public sealed record SkinnedBuildResult
{
    public required int VertexCount { get; init; }
    public required int TriangleCount { get; init; }
    public required IReadOnlyList<int> PartVertices { get; init; }
    public required IReadOnlyList<int> PartTriangles { get; init; }
    public required BoundsInfo Bounds { get; init; }
    public required float PlanViewExtentMeters { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

/// <summary>
/// Builds the animated counterpart of the static assembly: the body keeps its skeleton and skinning, attachments
/// are rigidly bound to their mount joints, and each MW5 clip becomes a named glTF animation.
/// </summary>
/// <remarks>
/// The body mesh is rebuilt from its accessors rather than round-tripped through <c>SceneBuilder.CreateFrom</c>,
/// because MW5 bodies carry eight UV sets and no material in the chain needs them: a skinned position/normal mesh
/// with one board material is smaller, deterministic and immune to SharpGLTF's four-UV limit.
/// Attachments are skinned vertices with weight 1 on their mount joint (not nodes parented to bones): Blender's
/// glTF importer handles the former exactly like the body and the latter unreliably.
/// </remarks>
public static class SkinnedModelBuilder
{
    public static SkinnedBuildResult Build(
        IReadOnlyList<SkinnedPart> parts,
        IReadOnlyList<ClipSample> clips,
        string outputPath)
    {
        var body = parts.FirstOrDefault(part => part.IsBody)
                   ?? throw new InvalidOperationException("Animated assembly needs a body part.");

        var source = ModelRoot.Load(body.GlbPath);
        var skinNode = source.LogicalNodes.FirstOrDefault(node => node.Skin is not null && node.Mesh is not null)
                       ?? throw new InvalidOperationException(
                           $"'{body.GlbPath}' has no skinned mesh; cannot build an animated model.");

        var skin = skinNode.Skin!;
        var joints = skin.Joints;
        var inverseBindMatrices = skin.InverseBindMatrices;

        var scene = new SceneBuilder();
        var armature = new NodeBuilder("Armature");
        scene.AddNode(armature);

        // Re-create the joint hierarchy: each joint hangs from its nearest joint ancestor, or from the armature.
        var jointSet = new HashSet<Node>(joints);
        var jointList = joints.ToList();
        var jointBuilders = new NodeBuilder[joints.Count];
        for (var index = 0; index < joints.Count; index++)
        {
            var builder = new NodeBuilder(string.IsNullOrWhiteSpace(joints[index].Name)
                ? $"joint-{index}"
                : joints[index].Name);
            builder.LocalTransform = joints[index].LocalTransform;
            jointBuilders[index] = builder;
        }

        for (var index = 0; index < joints.Count; index++)
        {
            var parent = joints[index].VisualParent;
            while (parent is not null && !jointSet.Contains(parent))
            {
                parent = parent.VisualParent;
            }

            if (parent is null)
            {
                armature.AddNode(jointBuilders[index]);
                continue;
            }

            var parentIndex = jointList.IndexOf(parent);
            if (parentIndex >= 0)
            {
                jointBuilders[parentIndex].AddNode(jointBuilders[index]);
            }
            else
            {
                armature.AddNode(jointBuilders[index]);
            }
        }

        var warnings = new List<string>();
        var partVertices = new int[parts.Count];
        var partTriangles = new int[parts.Count];
        var bounds = new BoundsAccumulator();

        // Body: a single skinned primitive, one board material, position/normal/joints/weights only.
        var bodyMaterial = new MaterialBuilder("board")
            .WithDoubleSide(true)
            .WithMetallicRoughness(0.1f, 0.85f);
        var bodyMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexJoints4>("assembled-body");
        var bodyPrimitive = bodyMesh.UsePrimitive(bodyMaterial);

        var bodyIndex = parts.ToList().FindIndex(part => part.IsBody);
        var bodyCounts = AppendSkinned(skinNode.Mesh!, bodyPrimitive, bounds);
        partVertices[bodyIndex] = bodyCounts.Vertices;
        partTriangles[bodyIndex] = bodyCounts.Triangles;

        var skinJoints = new (NodeBuilder Joint, Matrix4x4 InverseBindMatrix)[joints.Count];
        for (var index = 0; index < joints.Count; index++)
        {
            skinJoints[index] = (jointBuilders[index], inverseBindMatrices[index]);
        }

        scene.AddSkinnedMesh(bodyMesh, skinJoints);

        // Attachments: rigidly bound to their mount joint in the same skin as the body. They are *not* parented
        // to bone nodes: a node-parented mesh survives Blender's glTF importer unreliably, while a skinned vertex
        // with weight 1 on one joint behaves exactly like the body's own rigid skinning.
        var attachmentMaterial = new MaterialBuilder("board-attachments")
            .WithDoubleSide(true)
            .WithMetallicRoughness(0.1f, 0.85f);

        var attachmentMesh = new MeshBuilder<VertexPositionNormal, VertexEmpty, VertexJoints4>(
            "assembled-attachments");
        var attachmentPrimitive = attachmentMesh.UsePrimitive(attachmentMaterial);
        var attachments = 0;

        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            if (part.IsBody)
            {
                continue;
            }

            var jointIndex = FindJoint(joints, part.Bone);
            if (jointIndex < 0)
            {
                warnings.Add($"{part.Kind}: bone '{part.Bone}' not found in the body skeleton; part skipped");
                continue;
            }

            var counts = AppendRigid(
                attachmentPrimitive, part.GlbPath, part.Transform, jointIndex, bounds);

            partVertices[index] = counts.Vertices;
            partTriangles[index] = counts.Triangles;
            attachments += counts.Vertices;

            if (counts.Vertices == 0)
            {
                warnings.Add($"{part.Kind}: '{Path.GetFileName(part.GlbPath)}' exported without geometry");
            }
        }

        if (attachments > 0)
        {
            scene.AddSkinnedMesh(attachmentMesh, skinJoints);
        }

        // Deliberately *not* canonicalised: the whole rig stays in MW5's exported space (metres, Y-up, facing +X).
        // Blender's glTF importer drops an armature-level transform for the skinned mesh, so a baked basis would
        // leave the body and the attachments in different spaces. The driver applies `--model-yaw 90` instead.
        var model = scene.ToGltf2();
        var nodesByName = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in model.LogicalNodes.Where(node => !string.IsNullOrWhiteSpace(node.Name)))
        {
            nodesByName.TryAdd(node.Name, node);
        }

        foreach (var clip in clips)
        {
            var animation = model.CreateAnimation(clip.Entry.Name);
            var animated = 0;
            var skipped = 0;

            foreach (var (boneName, bone) in clip.Bones)
            {
                if (!nodesByName.TryGetValue(boneName, out var node))
                {
                    skipped++;
                    continue;
                }

                var frames = clip.Entry.SampledFrames > 0 ? clip.Entry.SampledFrames : clip.Entry.Frames;
                var fps = MathF.Max(1f, clip.Entry.Fps);

                var rotations = Channels.Rotation(bone, node, frames, fps);
                if (rotations is not null)
                {
                    animation.CreateRotationChannel(node, rotations);
                    animated++;
                }

                var translations = Channels.Translation(bone, node, frames, fps);
                if (translations is not null)
                {
                    animation.CreateTranslationChannel(node, translations);
                }

                var scales = Channels.Scale(bone, node, frames, fps);
                if (scales is not null)
                {
                    animation.CreateScaleChannel(node, scales);
                }
            }

            if (skipped > 0)
            {
                warnings.Add(
                    $"{clip.Entry.Name}: {skipped} animated bone(s) are not in the body skeleton and were ignored");
            }

            if (animated == 0)
            {
                warnings.Add($"{clip.Entry.Name}: no bone channel matched the body skeleton");
            }
        }

        model.SaveGLB(outputPath);

        var size = bounds.Size;
        return new SkinnedBuildResult
        {
            VertexCount = partVertices.Sum(),
            TriangleCount = partTriangles.Sum(),
            PartVertices = partVertices,
            PartTriangles = partTriangles,
            Bounds = new BoundsInfo
            {
                Origin = AssemblyReport.Vector(bounds.Center),
                Extent = AssemblyReport.Vector(size / 2f),
                SphereRadius = size.Length() / 2f
            },
            PlanViewExtentMeters = Math.Max(size.X, size.Z),
            Warnings = warnings
        };
    }

    private static int FindJoint(IReadOnlyList<Node> joints, string? bone)
    {
        if (string.IsNullOrWhiteSpace(bone))
        {
            return -1;
        }

        for (var index = 0; index < joints.Count; index++)
        {
            if (string.Equals(joints[index].Name, bone, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static (int Vertices, int Triangles) AppendSkinned(
        Mesh mesh,
        PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, VertexEmpty, VertexJoints4> primitive,
        BoundsAccumulator bounds)
    {
        var vertices = 0;
        var triangles = 0;

        foreach (var source in mesh.Primitives)
        {
            var positions = source.GetVertexAccessor("POSITION")?.AsVector3Array();
            var normals = source.GetVertexAccessor("NORMAL")?.AsVector3Array();
            var indices = source.GetIndices();
            if (positions is null || positions.Count == 0 || indices.Count < 3)
            {
                continue;
            }

            var skinning = ReadSkinning(source, positions.Count);
            var builders = new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexJoints4>[positions.Count];

            for (var vertex = 0; vertex < positions.Count; vertex++)
            {
                var position = positions[vertex];
                var normal = normals is not null && vertex < normals.Count
                    ? SafeNormalize(normals[vertex])
                    : Vector3.UnitY;

                builders[vertex] = new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexJoints4>(
                    new VertexPositionNormal(position, normal),
                    new VertexEmpty(),
                    skinning[vertex]);
                bounds.Add(position);
            }

            for (var triangle = 0; triangle + 2 < indices.Count; triangle += 3)
            {
                primitive.AddTriangle(
                    builders[indices[triangle]],
                    builders[indices[triangle + 1]],
                    builders[indices[triangle + 2]]);
            }

            vertices += positions.Count;
            triangles += indices.Count / 3;
        }

        return (vertices, triangles);
    }

    /// <summary>Reads JOINTS_0/WEIGHTS_0 into one <see cref="VertexJoints4"/> per vertex, honouring integer encodings.</summary>
    private static VertexJoints4[] ReadSkinning(MeshPrimitive primitive, int count)
    {
        var jointsAccessor = primitive.GetVertexAccessor("JOINTS_0")
                             ?? throw new InvalidOperationException("The body mesh has no JOINTS_0 attribute.");
        var weightsAccessor = primitive.GetVertexAccessor("WEIGHTS_0")
                              ?? throw new InvalidOperationException("The body mesh has no WEIGHTS_0 attribute.");

        var result = new VertexJoints4[count];
        for (var vertex = 0; vertex < count; vertex++)
        {
            var joints = AccessorReader.ReadVector4(jointsAccessor, vertex);
            var weights = AccessorReader.ReadVector4(weightsAccessor, vertex, normalize: true);

            var influences = new List<(int Joint, float Weight)>(4);
            var total = 0f;
            foreach (var (joint, weight) in new[]
                     {
                         ((int)joints.X, weights.X), ((int)joints.Y, weights.Y),
                         ((int)joints.Z, weights.Z), ((int)joints.W, weights.W)
                     })
            {
                if (weight > 0f)
                {
                    influences.Add((joint, weight));
                    total += weight;
                }
            }

            if (influences.Count == 0)
            {
                influences.Add((0, 1f));
                total = 1f;
            }

            // glTF requires weights to sum to 1; MW5's rigid skinning already does, keep it safe regardless.
            result[vertex] = new VertexJoints4(
                influences.Select(influence => (influence.Joint, influence.Weight / total)).ToArray());
        }

        return result;
    }

    private static (int Vertices, int Triangles) AppendRigid(
        PrimitiveBuilder<MaterialBuilder, VertexPositionNormal, VertexEmpty, VertexJoints4> primitive,
        string glbPath,
        Matrix4x4 transform,
        int jointIndex,
        BoundsAccumulator bounds)
    {
        var part = ModelRoot.Load(glbPath);
        var vertices = 0;
        var triangles = 0;
        var binding = new[] { (jointIndex, 1f) };

        foreach (var source in part.LogicalMeshes.SelectMany(mesh => mesh.Primitives))
        {
            var positions = source.GetVertexAccessor("POSITION")?.AsVector3Array();
            var normals = source.GetVertexAccessor("NORMAL")?.AsVector3Array();
            var indices = source.GetIndices();
            if (positions is null || positions.Count == 0 || indices.Count < 3)
            {
                continue;
            }

            var builders = new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexJoints4>[positions.Count];
            for (var vertex = 0; vertex < positions.Count; vertex++)
            {
                // The part is placed at its rest position in the body's exported space, then rigidly bound to
                // one joint: the joint's rest matrix cancels the bind, so the rest render is unchanged.
                var position = Vector3.Transform(positions[vertex], transform);
                var normal = normals is not null && vertex < normals.Count
                    ? SafeNormalize(Vector3.TransformNormal(normals[vertex], transform))
                    : Vector3.UnitY;

                builders[vertex] = new VertexBuilder<VertexPositionNormal, VertexEmpty, VertexJoints4>(
                    new VertexPositionNormal(position, normal),
                    new VertexEmpty(),
                    new VertexJoints4(binding));
                bounds.Add(position);
            }

            for (var triangle = 0; triangle + 2 < indices.Count; triangle += 3)
            {
                primitive.AddTriangle(
                    builders[indices[triangle]],
                    builders[indices[triangle + 1]],
                    builders[indices[triangle + 2]]);
            }

            vertices += positions.Count;
            triangles += indices.Count / 3;
        }

        return (vertices, triangles);
    }

    private static Vector3 SafeNormalize(Vector3 value) =>
        value.LengthSquared() < 1e-8f ? Vector3.UnitY : Vector3.Normalize(value);

    private sealed class BoundsAccumulator
    {
        private Vector3 _min = new(float.MaxValue);
        private Vector3 _max = new(float.MinValue);
        private bool _any;

        public void Add(Vector3 point)
        {
            _min = Vector3.Min(_min, point);
            _max = Vector3.Max(_max, point);
            _any = true;
        }

        public Vector3 Size => _any ? _max - _min : Vector3.Zero;

        public Vector3 Center => _any ? (_min + _max) / 2f : Vector3.Zero;

        public IEnumerable<Vector3> Corners()
        {
            if (!_any)
            {
                yield break;
            }

            yield return new Vector3(_min.X, _min.Y, _min.Z);
            yield return new Vector3(_min.X, _min.Y, _max.Z);
            yield return new Vector3(_min.X, _max.Y, _min.Z);
            yield return new Vector3(_min.X, _max.Y, _max.Z);
            yield return new Vector3(_max.X, _min.Y, _min.Z);
            yield return new Vector3(_max.X, _min.Y, _max.Z);
            yield return new Vector3(_max.X, _max.Y, _min.Z);
            yield return new Vector3(_max.X, _max.Y, _max.Z);
        }
    }
}

/// <summary>Reads a vertex attribute as four floats, including the integer encodings glTF allows for joints/weights.</summary>
internal static class AccessorReader
{
    public static Vector4 ReadVector4(Accessor accessor, int vertex, bool normalize = false)
    {
        var bytes = accessor.TryGetVertexBytes(vertex);
        if (bytes.Array is null)
        {
            return Vector4.Zero;
        }

        var span = bytes.AsSpan();
        return accessor.Encoding switch
        {
            EncodingType.FLOAT => new Vector4(
                BitConverter.ToSingle(span[0..4]), BitConverter.ToSingle(span[4..8]),
                BitConverter.ToSingle(span[8..12]), BitConverter.ToSingle(span[12..16])),
            EncodingType.UNSIGNED_BYTE => new Vector4(
                span[0], span[1], span[2], span[3]) / (normalize ? 255f : 1f),
            EncodingType.UNSIGNED_SHORT => new Vector4(
                BitConverter.ToUInt16(span[0..2]), BitConverter.ToUInt16(span[2..4]),
                BitConverter.ToUInt16(span[4..6]), BitConverter.ToUInt16(span[6..8])) / (normalize ? 65535f : 1f),
            _ => Vector4.Zero
        };
    }
}

/// <summary>Builds glTF animation sampler dictionaries, skipping channels that never leave the rest pose.</summary>
internal static class Channels
{
    private const float RotationTolerance = 0.001f;
    private const float TranslationTolerance = 1e-4f;

    public static Dictionary<float, Quaternion>? Rotation(
        SampledBone bone, Node node, int frames, float fps)
    {
        var rest = node.LocalTransform.Rotation;
        var keys = new Dictionary<float, Quaternion>(frames);
        var changed = false;
        var varies = false;
        for (var frame = 0; frame < frames; frame++)
        {
            var value = bone.Rotations[frame];
            keys[frame / fps] = value;
            changed |= MathF.Abs(Quaternion.Dot(rest, value)) < 1f - RotationTolerance;
            varies |= frame > 0 && MathF.Abs(Quaternion.Dot(bone.Rotations[0], value)) < 1f - RotationTolerance;
        }

        // A constant channel that still differs from the rest pose needs one key, not one per frame.
        return changed ? Collapse(keys, varies, fps) : null;
    }

    public static Dictionary<float, Vector3>? Translation(
        SampledBone bone, Node node, int frames, float fps)
    {
        if (bone.Translations is null)
        {
            return null;
        }

        var rest = node.LocalTransform.Translation;
        var keys = new Dictionary<float, Vector3>(frames);
        var changed = false;
        var varies = false;
        for (var frame = 0; frame < frames; frame++)
        {
            var value = bone.Translations[frame];
            keys[frame / fps] = value;
            changed |= Vector3.Distance(rest, value) > TranslationTolerance;
            varies |= frame > 0 && Vector3.Distance(bone.Translations[0], value) > TranslationTolerance;
        }

        return changed ? Collapse(keys, varies, fps) : null;
    }

    public static Dictionary<float, Vector3>? Scale(SampledBone bone, Node node, int frames, float fps)
    {
        if (bone.Scales is null)
        {
            return null;
        }

        var rest = node.LocalTransform.Scale;
        var keys = new Dictionary<float, Vector3>(frames);
        var changed = false;
        var varies = false;
        for (var frame = 0; frame < frames; frame++)
        {
            var value = bone.Scales[frame];
            keys[frame / fps] = value;
            changed |= Vector3.Distance(rest, value) > TranslationTolerance;
            varies |= frame > 0 && Vector3.Distance(bone.Scales[0], value) > TranslationTolerance;
        }

        return changed ? Collapse(keys, varies, fps) : null;
    }

    private static Dictionary<float, T> Collapse<T>(Dictionary<float, T> keys, bool varies, float fps) =>
        varies ? keys : new Dictionary<float, T> { [0f] = keys[0f] };
}
