using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.Utils;
using CUE4Parse_Conversion.Animations;
using CUE4Parse_Conversion.Writers.ActorX.Structs.Animations;
using MakaMek.Tools.MW5Extractor.Reporting;

namespace MakaMek.Tools.MW5Extractor.Animation;

/// <summary>
/// Decodes one <c>UAnimSequence</c> into per-frame bone transforms in glTF space.
/// </summary>
/// <remarks>
/// The conversion mirrors CUE4Parse's glTF mesh exporter (<c>Gltf.CreateGltfSkeleton</c>): a y/z swap with a
/// negated quaternion W, translations scaled from centimetres to metres. Applying the same conversion to the
/// animation keys is what makes the clip bind to the exported body skeleton without retargeting.
/// </remarks>
public static class AnimationClipReader
{
    /// <summary>A loop is treated as closed when its last key duplicates the first within these tolerances.</summary>
    private const float LoopClosureMeters = 0.005f;
    private const float LoopClosureDegrees = 0.5f;

    /// <summary>Root travel above this is stripped so the sprite stays anchored in its hex.</summary>
    private const float RootMotionToleranceMeters = 0.005f;

    public static ClipSample Read(
        UnrealArchive archive,
        ClipDefinition definition,
        string assetPath,
        int sourceFrames = 0)
    {
        var asset = archive.LoadObject<UAnimSequence>(assetPath);
        var skeleton = LoadSkeleton(asset, assetPath);

        var boneNames = skeleton.ReferenceSkeleton.FinalRefBoneInfo
            .Select((bone, index) => (Name: bone.Name.Text, Parent: bone.ParentIndex, Index: index))
            .ToArray();

        var converted = asset.ConvertAnims();
        var sequence = converted.Sequences.FirstOrDefault()
                       ?? throw new InvalidOperationException($"'{assetPath}' produced no animation sequence.");

        var frameCount = sourceFrames > 0 ? Math.Min(sourceFrames, sequence.NumFrames) : sequence.NumFrames;
        if (frameCount < 2)
        {
            throw new InvalidOperationException($"'{assetPath}' has {frameCount} frame(s); nothing to animate.");
        }

        var length = sequence.AnimEndTime > 0 ? sequence.AnimEndTime : sequence.OriginalSequence.SequenceLength;
        if (length <= 0)
        {
            throw new InvalidOperationException($"'{assetPath}' has no sequence length.");
        }

        if (sequence.Tracks.Count < boneNames.Length)
        {
            throw new InvalidOperationException(
                $"'{assetPath}' exposes {sequence.Tracks.Count} tracks for {boneNames.Length} skeleton bones.");
        }

        var bones = new Dictionary<string, SampledBone>(StringComparer.OrdinalIgnoreCase);
        var referencePose = skeleton.ReferenceSkeleton.FinalRefBonePose;
        for (var index = 0; index < boneNames.Length; index++)
        {
            // A track that only carries translation/scale keys leaves rotation untouched in GetBoneTransform, so it
            // must start from the reference pose - starting from identity would override the bind-pose rotation.
            var referenceRotation = index < referencePose.Length
                ? referencePose[index].Rotation
                : new CUE4Parse.UE4.Objects.Core.Math.FQuat(0, 0, 0, 1);

            var bone = ReadBone(sequence.Tracks[index], frameCount, referenceRotation);
            if (bone is not null)
            {
                bones[boneNames[index].Name] = bone;
            }
        }

        var rootIndex = Array.FindIndex(boneNames, bone => bone.Parent < 0);
        var rootName = rootIndex >= 0 ? boneNames[rootIndex].Name : boneNames[0].Name;

        var warnings = new List<string>();

        // MW5 clips are keyed at every source frame and the last key sits exactly at SequenceLength, so the sampling
        // interval is (N-1)/length - not N/length, which would compress the clip by one frame.
        var inputFps = frameCount > 1 ? (frameCount - 1) / length : 1f;

        var rootMotion = RootMotion(bones, rootName, frameCount);
        if (rootMotion > RootMotionToleranceMeters)
        {
            StripRootDrift(bones, rootName, frameCount);
            warnings.Add(
                $"{definition.Name}: stripped {rootMotion:0.000} m of root travel from '{rootName}' " +
                "(the sprite is board-anchored).");
        }

        // A looping clip repeats its first pose at SequenceLength; rendering both would double the pose for one
        // shipped frame, so the closing key is dropped. Non-loops keep every frame.
        var duplicateLast = definition.Loop && frameCount > 2;
        var sampledFrames = duplicateLast ? frameCount - 1 : frameCount;

        if (definition.Loop && !IsLoopClosed(bones, frameCount))
        {
            warnings.Add(
                $"{definition.Name}: loop does not close ({MaxClosureDegrees(bones, frameCount):0.0}° / " +
                $"{MaxClosureMeters(bones, frameCount):0.000} m between first and last key); the sprite loop will jump.");
        }

        return new ClipSample
        {
            Entry = new ClipEntry
            {
                Name = definition.Name,
                Status = "available",
                AssetPath = assetPath,
                Candidates = [assetPath],
                Loop = definition.Loop,
                Hold = definition.Hold,
                Frames = frameCount,
                SampledFrames = sampledFrames,
                Fps = MathF.Round(inputFps, 4),
                DurationSeconds = MathF.Round(length, 4),
                AnimatedBones = bones.Count,
                RootMotionMeters = MathF.Round(rootMotion, 4),
                LoopClosureDegrees = MathF.Round(MaxClosureDegrees(bones, frameCount), 4),
                LoopClosureMeters = MathF.Round(MaxClosureMeters(bones, frameCount), 4),
                Skeleton = skeleton.Name,
                Warnings = warnings
            },
            Bones = bones
        };
    }

    private static USkeleton LoadSkeleton(UAnimSequence asset, string assetPath)
    {
        if (asset.Skeleton is null || !asset.Skeleton.TryLoad<USkeleton>(out var skeleton))
        {
            throw new InvalidOperationException($"'{assetPath}' has no loadable skeleton.");
        }

        return skeleton;
    }

    private static SampledBone? ReadBone(
        CAnimTrack track,
        int frameCount,
        CUE4Parse.UE4.Objects.Core.Math.FQuat referenceRotation)
    {
        var hasRotation = track.KeyQuat.Length > 0;
        var hasTranslation = track.KeyPos.Length > 0;
        var hasScale = track.KeyScale.Length > 0;
        if (!hasRotation && !hasTranslation && !hasScale)
        {
            // The clip does not animate this bone: leaving the rest transform alone is correct.
            return null;
        }

        var rotations = new Quaternion[frameCount];
        var translations = hasTranslation ? new Vector3[frameCount] : null;
        var scales = hasScale ? new Vector3[frameCount] : null;

        var rotation = referenceRotation;
        var translation = new CUE4Parse.UE4.Objects.Core.Math.FVector(0, 0, 0);
        var scale = new CUE4Parse.UE4.Objects.Core.Math.FVector(1, 1, 1);

        for (var frame = 0; frame < frameCount; frame++)
        {
            translation = new CUE4Parse.UE4.Objects.Core.Math.FVector(0, 0, 0);
            scale = new CUE4Parse.UE4.Objects.Core.Math.FVector(1, 1, 1);

            track.GetBoneTransform(frame, frameCount, ref rotation, ref translation, ref scale);

            rotations[frame] = SwapYz(rotation);
            if (translations is not null)
            {
                translations[frame] = SwapYz(translation) * 0.01f;
            }

            if (scales is not null)
            {
                scales[frame] = SwapYz(scale);
            }
        }

        return new SampledBone
        {
            Rotations = rotations,
            Translations = translations,
            Scales = scales
        };
    }

    /// <summary>UE (Z-up, left-handed) → glTF (Y-up): the same basis change CUE4Parse applies to bone transforms.</summary>
    private static Quaternion SwapYz(CUE4Parse.UE4.Objects.Core.Math.FQuat quat) =>
        Quaternion.Normalize(new Quaternion(quat.X, quat.Z, quat.Y, -quat.W));

    private static Vector3 SwapYz(CUE4Parse.UE4.Objects.Core.Math.FVector vector) =>
        new(vector.X, vector.Z, vector.Y);

    /// <summary>True when the last key reproduces the first pose, i.e. the closing frame of a loop.</summary>
    private static bool IsLoopClosed(IReadOnlyDictionary<string, SampledBone> bones, int frameCount)
    {
        if (frameCount < 3 || bones.Count == 0)
        {
            return false;
        }

        var last = frameCount - 1;
        foreach (var bone in bones.Values)
        {
            if (bone.Translations is not null &&
                Vector3.Distance(bone.Translations[0], bone.Translations[last]) > LoopClosureMeters)
            {
                return false;
            }

            if (AngleDegrees(bone.Rotations[0], bone.Rotations[last]) > LoopClosureDegrees)
            {
                return false;
            }
        }

        return true;
    }

    private static float MaxClosureDegrees(IReadOnlyDictionary<string, SampledBone> bones, int frameCount)
    {
        if (frameCount < 2)
        {
            return 0;
        }

        var last = frameCount - 1;
        var maximum = 0f;
        foreach (var bone in bones.Values)
        {
            maximum = MathF.Max(maximum, AngleDegrees(bone.Rotations[0], bone.Rotations[last]));
        }

        return maximum;
    }

    private static float MaxClosureMeters(IReadOnlyDictionary<string, SampledBone> bones, int frameCount)
    {
        if (frameCount < 2)
        {
            return 0;
        }

        var last = frameCount - 1;
        var maximum = 0f;
        foreach (var bone in bones.Values)
        {
            if (bone.Translations is not null)
            {
                maximum = MathF.Max(maximum, Vector3.Distance(bone.Translations[0], bone.Translations[last]));
            }
        }

        return maximum;
    }

    private static float AngleDegrees(Quaternion a, Quaternion b)
    {
        var dot = MathF.Abs(Quaternion.Dot(a, b));
        var clamped = MathF.Min(1f, dot);
        return MathF.Acos(clamped) * 2f * (180f / MathF.PI);
    }

    /// <summary>Largest horizontal displacement of the root bone over the clip, in metres.</summary>
    private static float RootMotion(IReadOnlyDictionary<string, SampledBone> bones, string root, int frameCount)
    {
        if (frameCount < 2 || !bones.TryGetValue(root, out var bone) || bone.Translations is null)
        {
            return 0;
        }

        var first = bone.Translations[0];
        var maximum = 0f;
        for (var frame = 1; frame < frameCount; frame++)
        {
            var delta = bone.Translations[frame] - first;
            maximum = MathF.Max(maximum, MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z));
        }

        return maximum;
    }

    private static void StripRootDrift(Dictionary<string, SampledBone> bones, string root, int frameCount)
    {
        if (!bones.TryGetValue(root, out var bone) || bone.Translations is null)
        {
            return;
        }

        var first = bone.Translations[0];
        var stripped = new Vector3[frameCount];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var value = bone.Translations[frame];
            stripped[frame] = new Vector3(first.X, value.Y, first.Z);
        }

        bones[root] = bone with { Translations = stripped };
    }
}
