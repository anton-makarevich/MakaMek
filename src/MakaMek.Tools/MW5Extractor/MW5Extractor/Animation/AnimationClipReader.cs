using System.Numerics;
using CUE4Parse.UE4.Assets.Exports.Animation;
using CUE4Parse.UE4.Objects.Core.Math;
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
        int sourceFrames = 0,
        int shippedFrames = 10)
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
        var sampledBones = new SampledBone?[boneNames.Length];
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
                sampledBones[index] = bone;
                bones[boneNames[index].Name] = bone;
            }
        }

        var rootIndex = Array.FindIndex(boneNames, bone => bone.Parent < 0);
        var rootName = rootIndex >= 0 ? boneNames[rootIndex].Name : boneNames[0].Name;

        var warnings = new List<string>();

        // MW5 clips are keyed at every source frame and the last key sits exactly at SequenceLength, so the sampling
        // interval is (N-1)/length - not N/length, which would compress the clip by one frame.
        var inputFps = frameCount > 1 ? (frameCount - 1) / length : 1f;

        // Capture the authored root trajectory before stripping it: the endpoint displacement is the
        // stride that drives the runtime distance-per-cycle calibration. RootMotionMeters alone is only
        // the maximum excursion and overstates the stride whenever the root also sways sideways.
        var trajectory = AnalyzeRootTrajectory(bones, rootName, frameCount);
        var rootMotion = trajectory?.MaxExcursionMeters ?? 0;

        if (rootMotion > RootMotionToleranceMeters)
        {
            StripRootDrift(bones, rootName, frameCount);
            warnings.Add(
                $"{definition.Name}: stripped {rootMotion:0.000} m of root travel from '{rootName}' " +
                "(the sprite is board-anchored).");
        }

        if (trajectory is { } root)
        {
            if (!root.IsMonotonic)
            {
                warnings.Add(
                    $"{definition.Name}: root progress is not monotonic; the stride (endpoint " +
                    $"{root.EndpointDisplacementMeters:0.000} m) may not match the visible gait.");
            }

            if (rootMotion > RootMotionToleranceMeters &&
                MathF.Abs(root.EndpointDisplacementMeters - rootMotion) > rootMotion * 0.1f)
            {
                warnings.Add(
                    $"{definition.Name}: endpoint displacement ({root.EndpointDisplacementMeters:0.000} m) " +
                    $"differs from maximum root excursion ({rootMotion:0.000} m); " +
                    "the clip may include lateral drift.");
            }
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

        var footGait = AnalyzeFootGait(
            asset, boneNames, referencePose, sampledBones, rootIndex, frameCount, length, inputFps,
            trajectory, shippedFrames);
        if (footGait is not null)
        {
            warnings.AddRange(footGait.Warnings);
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
                RootEndpointDisplacementMeters = MathF.Round(trajectory?.EndpointDisplacementMeters ?? 0, 4),
                RootDisplacementXMeters = MathF.Round(trajectory?.Displacement.X ?? 0, 4),
                RootDisplacementZMeters = MathF.Round(trajectory?.Displacement.Z ?? 0, 4),
                RootProgressMonotonic = trajectory is null || trajectory.Value.IsMonotonic,
                RootMaxLateralDeviationMeters = MathF.Round(trajectory?.MaxLateralDeviationMeters ?? 0, 4),
                RootPathLengthMeters = MathF.Round(trajectory?.PathLengthMeters ?? 0, 4),
                FootGait = footGait,
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

    /// <summary>
    /// Pre-strip diagnostics of the root bone's horizontal trajectory.
    /// </summary>
    private readonly record struct RootTrajectory(
        Vector3 Displacement,
        float EndpointDisplacementMeters,
        float MaxExcursionMeters,
        float PathLengthMeters,
        float MaxLateralDeviationMeters,
        bool IsMonotonic);

    /// <summary>
    /// Measures the horizontal root trajectory before <see cref="StripRootDrift"/> removes it.
    /// The endpoint displacement is the authored stride; the excursion is the largest distance from the
    /// first key and, like the old <c>rootMotionMeters</c>, includes any sideways sway.
    /// </summary>
    private static RootTrajectory? AnalyzeRootTrajectory(
        IReadOnlyDictionary<string, SampledBone> bones,
        string root,
        int frameCount)
    {
        if (frameCount < 2 || !bones.TryGetValue(root, out var bone) || bone.Translations is null)
        {
            return null;
        }

        var first = bone.Translations[0];
        var last = bone.Translations[frameCount - 1];
        var displacement = new Vector3(last.X - first.X, 0, last.Z - first.Z);
        var endpoint = displacement.Length();
        var forward = endpoint > 1e-6f ? displacement / endpoint : Vector3.Zero;

        var maximumExcursion = 0f;
        var pathLength = 0f;
        var maximumLateralDeviation = 0f;
        var isMonotonic = true;

        for (var frame = 1; frame < frameCount; frame++)
        {
            var previous = bone.Translations[frame - 1];
            var current = bone.Translations[frame];

            var stepX = current.X - previous.X;
            var stepZ = current.Z - previous.Z;
            pathLength += MathF.Sqrt(stepX * stepX + stepZ * stepZ);

            var fromFirstX = current.X - first.X;
            var fromFirstZ = current.Z - first.Z;
            maximumExcursion = MathF.Max(maximumExcursion, MathF.Sqrt(fromFirstX * fromFirstX + fromFirstZ * fromFirstZ));

            if (endpoint > 1e-6f)
            {
                // Signed progress along the displacement line: a negative step means the root moved
                // backwards, so the clip's root path cannot drive a clean stride.
                var progress = stepX * forward.X + stepZ * forward.Z;
                if (progress < -1e-4f)
                {
                    isMonotonic = false;
                }

                // Perpendicular distance from the first-to-last line, in the XZ plane.
                var lateral = MathF.Abs(fromFirstX * forward.Z - fromFirstZ * forward.X);
                maximumLateralDeviation = MathF.Max(maximumLateralDeviation, lateral);
            }
        }

        return new RootTrajectory(
            displacement,
            endpoint,
            maximumExcursion,
            pathLength,
            maximumLateralDeviation,
            isMonotonic);
    }

    /// <summary>
    /// Measures the foot-down notify windows using only the frames the sprite actually ships, so the
    /// result describes what the player sees rather than the 30 fps source. It never changes the
    /// strip/sampling behaviour; the corrected stride is a diagnostic value.
    /// </summary>
    private static FootGaitMetrics? AnalyzeFootGait(
        UAnimSequence asset,
        (string Name, int Parent, int Index)[] boneNames,
        FTransform[] referencePose,
        SampledBone?[] sampledBones,
        int rootIndex,
        int frameCount,
        float length,
        float fps,
        RootTrajectory? trajectory,
        int shippedFrames)
    {
        if (trajectory is not { } root || rootIndex < 0)
        {
            return null;
        }

        var notifies = (asset.Notifies ?? [])
            .Where(notify => notify.NotifyName?.Text is "LeftFoot_Down" or "RightFoot_Down")
            .Select(notify => (
                Foot: notify.NotifyName?.Text == "LeftFoot_Down" ? "Left" : "Right",
                Start: (double)notify.LinkValue,
                Duration: (double)notify.Duration))
            .Where(notify => notify.Duration > 0 && notify.Duration < length)
            .OrderBy(notify => notify.Start)
            .ToList();

        if (notifies.Count == 0)
        {
            return null;
        }

        var leftFootIndex = FindBoneIndex(boneNames, "Foot0_Left", "ToeIndex0_Left", "Foot1_Left");
        var rightFootIndex = FindBoneIndex(boneNames, "Foot0_Right", "ToeIndex0_Right", "Foot1_Right");
        if (leftFootIndex < 0 && rightFootIndex < 0)
        {
            return null;
        }

        var sampledFrameCount = Math.Clamp(shippedFrames, 2, frameCount);
        var sampledFrameIndices = Enumerable.Range(0, sampledFrameCount)
            .Select(index => (int)Math.Round(index * (frameCount - 1) / (double)sampledFrameCount))
            .ToArray();
        var sampledTimes = sampledFrameIndices.Select(index => (double)index / fps).ToArray();

        var rootPositions = new Vector3[sampledFrameCount];
        var leftFootPositions = new Vector3[sampledFrameCount];
        var rightFootPositions = new Vector3[sampledFrameCount];
        for (var sample = 0; sample < sampledFrameCount; sample++)
        {
            var world = ComputeWorldPositions(
                sampledFrameIndices[sample], boneNames, referencePose, sampledBones,
                rootIndex, leftFootIndex, rightFootIndex);
            rootPositions[sample] = world.Root;
            leftFootPositions[sample] = world.LeftFoot;
            rightFootPositions[sample] = world.RightFoot;
        }

        var forward = new Vector3(root.Displacement.X, 0, root.Displacement.Z);
        forward = forward.LengthSquared() > 1e-8f ? Vector3.Normalize(forward) : Vector3.Zero;

        var contacts = new List<FootContactMetric>(notifies.Count);
        foreach (var notify in notifies)
        {
            var footIndex = notify.Foot == "Left" ? leftFootIndex : rightFootIndex;
            if (footIndex < 0) continue;

            var footPositions = notify.Foot == "Left" ? leftFootPositions : rightFootPositions;
            var start = Math.Clamp(notify.Start, 0, length);
            var end = Math.Clamp(notify.Start + notify.Duration, 0, length);

            var first = Array.FindIndex(sampledTimes, time => time >= start);
            var last = Array.FindLastIndex(sampledTimes, time => time <= end);
            if (first < 0 || last < first) continue;

            var footDelta = footPositions[last] - footPositions[first];
            footDelta.Y = 0;
            var rootDelta = rootPositions[last] - rootPositions[first];
            rootDelta.Y = 0;

            contacts.Add(new FootContactMetric
            {
                Foot = notify.Foot,
                Bone = boneNames[footIndex].Name,
                StartSeconds = MathF.Round((float)notify.Start, 4),
                DurationSeconds = MathF.Round((float)notify.Duration, 4),
                RootTravelMeters = MathF.Round(rootDelta.Length(), 4),
                FootTravelMeters = MathF.Round(footDelta.Length(), 4),
                ForwardSlipMeters = MathF.Round(Vector3.Dot(footDelta, forward), 4),
                SampleCount = last - first + 1
            });
        }

        if (contacts.Count == 0)
        {
            return null;
        }

        var sumRootTravel = contacts.Sum(contact => contact.RootTravelMeters);
        var sumIdealTravel = contacts.Sum(contact => contact.RootTravelMeters - contact.ForwardSlipMeters);
        var correctedStride = sumRootTravel > 1e-6f
            ? root.EndpointDisplacementMeters * (sumIdealTravel / sumRootTravel)
            : root.EndpointDisplacementMeters;

        var warnings = new List<string>();
        if (contacts.All(contact => contact.SampleCount < 2))
        {
            warnings.Add(
                $"{asset.Name}: every stance window contains fewer than two shipped frames; the sampled " +
                "foot probe cannot measure slip.");
        }

        if (correctedStride <= 0)
        {
            warnings.Add(
                $"{asset.Name}: the sampled foot slip suggests a non-positive stride; ignore it and use " +
                "the root endpoint.");
        }

        return new FootGaitMetrics
        {
            Frames = sampledFrameCount,
            Contacts = contacts,
            CorrectedStrideMeters = MathF.Round(correctedStride, 4),
            Warnings = warnings
        };
    }

    private static int FindBoneIndex(
        (string Name, int Parent, int Index)[] boneNames,
        params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var index = Array.FindIndex(boneNames,
                bone => string.Equals(bone.Name, candidate, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) return index;
        }

        return -1;
    }

    private readonly record struct FootWorldPositions(Vector3 Root, Vector3 LeftFoot, Vector3 RightFoot);

    /// <summary>
    /// Composes the local bone transforms (sampled where present, reference pose otherwise) into world
    /// positions for the root and the two foot bones.
    /// </summary>
    private static FootWorldPositions ComputeWorldPositions(
        int frame,
        (string Name, int Parent, int Index)[] boneNames,
        FTransform[] referencePose,
        SampledBone?[] sampledBones,
        int rootIndex,
        int leftFootIndex,
        int rightFootIndex)
    {
        var count = boneNames.Length;
        var worldPositions = new Vector3[count];
        var worldRotations = new Quaternion[count];
        var worldScales = new Vector3[count];
        var computed = new bool[count];

        void EnsureComputed(int index)
        {
            if (computed[index]) return;

            var reference = index < referencePose.Length ? referencePose[index] : default;
            var translation = index < referencePose.Length
                ? SwapYz(reference.Translation) * 0.01f
                : Vector3.Zero;
            var rotation = index < referencePose.Length
                ? SwapYz(reference.Rotation)
                : Quaternion.Identity;
            var scale = index < referencePose.Length
                ? SwapYz(reference.Scale3D)
                : Vector3.One;

            if (sampledBones[index] is { } sampled)
            {
                rotation = sampled.Rotations[frame];
                if (sampled.Translations is { } translations) translation = translations[frame];
                if (sampled.Scales is { } scales) scale = scales[frame];
            }

            var parent = boneNames[index].Parent;
            if (parent < 0)
            {
                worldPositions[index] = translation;
                worldRotations[index] = rotation;
                worldScales[index] = scale;
            }
            else
            {
                EnsureComputed(parent);
                worldPositions[index] =
                    worldPositions[parent] + Vector3.Transform(translation * worldScales[parent], worldRotations[parent]);
                worldRotations[index] = Quaternion.Normalize(worldRotations[parent] * rotation);
                worldScales[index] = worldScales[parent] * scale;
            }

            computed[index] = true;
        }

        EnsureComputed(rootIndex);
        if (leftFootIndex >= 0) EnsureComputed(leftFootIndex);
        if (rightFootIndex >= 0) EnsureComputed(rightFootIndex);

        return new FootWorldPositions(
            worldPositions[rootIndex],
            leftFootIndex >= 0 ? worldPositions[leftFootIndex] : Vector3.Zero,
            rightFootIndex >= 0 ? worldPositions[rightFootIndex] : Vector3.Zero);
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
