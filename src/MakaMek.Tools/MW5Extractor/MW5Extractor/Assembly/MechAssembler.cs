using System.Diagnostics;
using System.Numerics;
using MakaMek.Tools.MW5Extractor.Discovery;
using MakaMek.Tools.MW5Extractor.Extraction;
using MakaMek.Tools.MW5Extractor.Inspection;
using MakaMek.Tools.MW5Extractor.Reporting;
using SharpGLTF.Schema2;

namespace MakaMek.Tools.MW5Extractor.Assembly;

/// <summary>
/// Builds one renderable model per variant: the chassis body, its cockpit and glass, and the weapon meshes
/// its loadout asks for, merged into a single glTF in canonical space.
/// </summary>
/// <remarks>
/// The mapping is data, not code: the loadout names the slot and the weapon data asset, the weapon data asset
/// carries the weapon tag, the hardpoint set maps that tag to the weapon Blueprint, and the hardpoint set's
/// socket names the bone the weapon hangs from. Anything that cannot be resolved is reported, never dropped.
/// </remarks>
public static class MechAssembler
{
    /// <summary>Unreal's <c>/Game/</c> maps to this content root inside the pak.</summary>
    private const string GameContentPrefix = "MW5Mercs/Content/";

    /// <summary>Row-vector basis change: (x, y, z)_UE (Z-up) → (x, z, −y)_glTF (Y-up).</summary>
    private static readonly Matrix4x4 UeToGltf = new(
        1, 0, 0, 0,
        0, 0, -1, 0,
        0, 1, 0, 0,
        0, 0, 0, 1);

    public static async Task<AssemblyReport> AssembleAsync(
        UnrealArchive archive,
        AssemblyRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();
        var unmapped = new List<string>();

        var discovery = ChassisDiscovery.Build(archive, request.Chassis);
        var chassis = SelectChassis(discovery.Chassis, request.Chassis);
        if (chassis.Body is null)
        {
            throw new InvalidOperationException($"Chassis '{chassis.Folder}' has no body mesh; cannot assemble.");
        }

        var requestedVariant = request.Variant;
        if (requestedVariant is null &&
            discovery.Chassis.Any(candidate => candidate.Variants.Any(v => Equals(v.Id, request.Chassis))))
        {
            // `assemble AS7-D` means the variant, not "every AS7-* variant".
            requestedVariant = request.Chassis;
        }

        var variant = SelectVariant(chassis, requestedVariant);
        if (variant is null)
        {
            throw new InvalidOperationException($"Chassis '{chassis.Folder}' has no variant with a loadout.");
        }

        if (variant.Loadout is null)
        {
            warnings.Add($"variant '{variant.Id}' has no loadout asset; weapons will be missing");
        }

        var outputDirectory = Path.GetFullPath(request.OutputDirectory);
        var partsDirectory = Path.Combine(outputDirectory, "parts");
        var dataDirectory = Path.Combine(outputDirectory, "data");
        Directory.CreateDirectory(partsDirectory);
        Directory.CreateDirectory(dataDirectory);

        var plan = new List<PlannedPart>
        {
            new() { Kind = "body", Asset = chassis.Body, Transform = Matrix4x4.Identity }
        };

        var bodyGlb = await ExportMesh(archive, chassis.Body, partsDirectory, cancellationToken);
        var boneMatrices = ReadBoneMatrices(bodyGlb);
        var socketBones = ReadSocketBones(archive, chassis.Body);

        if (!request.SkipCockpit)
        {
            var cockpitPlanned = await AddCockpitAsync(
                archive, chassis, boneMatrices, dataDirectory, plan, warnings, cancellationToken);

            if (!cockpitPlanned && chassis.Cockpit is not null)
            {
                var cockpitLocal = await ReadCockpitLocalAsync(archive, chassis.Cockpit, dataDirectory, warnings, cancellationToken);
                var cockpitMatrix = cockpitLocal * Bone(boneMatrices, "Cockpit", warnings);

                plan.Add(new PlannedPart
                {
                    Kind = "cockpit", Asset = chassis.Cockpit, Socket = "Cockpit", Bone = "Cockpit", Transform = cockpitMatrix
                });

                if (chassis.CockpitGlass is not null)
                {
                    // MW5 attaches the glass to the cockpit mesh component, so it inherits its transform.
                    plan.Add(new PlannedPart
                    {
                        Kind = "glass", Asset = chassis.CockpitGlass, Socket = "Cockpit", Bone = "Cockpit", Transform = cockpitMatrix
                    });
                }
            }
        }

        if (!request.SkipWeapons && chassis.HardpointSet is not null && variant.Loadout is not null)
        {
            await AddWeaponsAsync(
                archive, chassis, variant, socketBones, boneMatrices, dataDirectory,
                plan, unmapped, warnings, cancellationToken);
        }

        var mergeParts = new List<MergePart>();
        foreach (var item in plan)
        {
            var glb = item.Kind == "body"
                ? bodyGlb
                : await ExportMesh(archive, item.Asset, partsDirectory, cancellationToken);
            mergeParts.Add(new MergePart { Label = item.Kind, GlbPath = glb, Transform = item.Transform });
        }

        var modelPath = Path.Combine(outputDirectory, $"{variant.Id}.glb");
        var merged = ModelMerger.Merge(mergeParts, modelPath);

        var parts = plan.Select((item, index) => new AssemblyPart
        {
            Kind = item.Kind,
            AssetPath = item.Asset,
            Slot = item.Slot,
            Socket = item.Socket,
            Bone = item.Bone,
            WeaponTag = item.Tag,
            Translation = AssemblyReport.Vector(item.Transform.Translation),
            GlbPath = mergeParts[index].GlbPath,
            VertexCount = merged.PartVertices[index],
            TriangleCount = merged.PartTriangles[index]
        }).ToList();

        stopwatch.Stop();

        return new AssemblyReport
        {
            Chassis = chassis.Folder,
            Variant = variant.Id,
            ModelPath = modelPath,
            VertexCount = merged.VertexCount,
            TriangleCount = merged.TriangleCount,
            Bounds = merged.Bounds,
            PlanViewExtentMeters = merged.PlanViewExtentMeters,
            Parts = parts,
            Unmapped = unmapped,
            Warnings = warnings,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
        };
    }

    /// <summary>Adds the visible (third-person) cockpit from the chassis model data asset, if it declares one.</summary>
    private static async Task<bool> AddCockpitAsync(
        UnrealArchive archive,
        ChassisEntry chassis,
        IReadOnlyDictionary<string, Matrix4x4> boneMatrices,
        string dataDirectory,
        List<PlannedPart> plan,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (chassis.ModelDefinition is null)
        {
            return false;
        }

        var modelJson = await ExportJson(archive, chassis.ModelDefinition, dataDirectory, cancellationToken);
        var cockpit = UnrealDataReader.ReadThirdPersonCockpit(modelJson);
        if (cockpit.MeshAssetPath is null)
        {
            warnings.Add("model data asset declares no third-person cockpit mesh; falling back to the first-person cockpit");
            return false;
        }

        var mesh = GamePathToVirtual(cockpit.MeshAssetPath);
        if (archive.FindFile(mesh) is null)
        {
            warnings.Add($"third-person cockpit mesh '{mesh}' not found; falling back to the first-person cockpit");
            return false;
        }

        plan.Add(new PlannedPart
        {
            Kind = "cockpit",
            Asset = mesh,
            Socket = cockpit.BoneName,
            Bone = cockpit.BoneName,
            Transform = Bone(boneMatrices, cockpit.BoneName, warnings)
        });

        return true;
    }

    private static async Task AddWeaponsAsync(
        UnrealArchive archive,
        ChassisEntry chassis,
        VariantEntry variant,
        IReadOnlyDictionary<string, string> socketBones,
        IReadOnlyDictionary<string, Matrix4x4> boneMatrices,
        string dataDirectory,
        List<PlannedPart> plan,
        List<string> unmapped,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var hardpointsFile = await ExportJson(archive, chassis.HardpointSet!, dataDirectory, cancellationToken);
        var loadoutFile = await ExportJson(archive, variant.Loadout!, dataDirectory, cancellationToken);

        var slots = UnrealDataReader.ReadHardpoints(hardpointsFile);
        var installed = UnrealDataReader.ReadLoadout(loadoutFile);
        if (slots.Count == 0)
        {
            unmapped.Add($"{chassis.Folder}: hardpoint set exported without weapon slots");
            return;
        }

        foreach (var weapon in installed)
        {
            if (string.IsNullOrWhiteSpace(weapon.WeaponAsset) || weapon.WeaponAsset == "None")
            {
                unmapped.Add($"{weapon.SlotId}: not a visual weapon (actuator / melee / empty)");
                continue;
            }

            var weaponAsset = archive.FindFile(weapon.WeaponAsset);
            if (weaponAsset is null)
            {
                unmapped.Add($"{weapon.SlotId}: weapon data asset '{weapon.WeaponAsset}' not found");
                continue;
            }

            var weaponJson = await ExportJson(archive, weaponAsset.PathWithoutExtension, dataDirectory, cancellationToken);
            var weaponInfo = UnrealDataReader.ReadWeapon(weaponJson);

            var slot = slots.FirstOrDefault(candidate => Equals(candidate.SlotId, weapon.SlotId))
                       ?? slots.FirstOrDefault(candidate =>
                           SameHardpoint(candidate.SlotId, weapon.SlotId) &&
                           Equals(candidate.SlotType, weaponInfo.SlotType));

            if (slot is null)
            {
                unmapped.Add($"{weapon.SlotId}: no hardpoint slot matches");
                continue;
            }

            var visual = ResolveVisual(slot, weaponInfo, weapon.SlotId, unmapped, warnings);
            if (visual is null)
            {
                continue;
            }

            var meshPath = VisualToMeshPath(visual);
            if (archive.FindFile(meshPath) is null)
            {
                unmapped.Add($"{weapon.SlotId}: visual mesh '{meshPath}' not found");
                continue;
            }

            var bone = slot.Socket is not null ? socketBones.GetValueOrDefault(slot.Socket) : null;
            if (bone is null || !boneMatrices.TryGetValue(bone, out var boneMatrix))
            {
                unmapped.Add($"{weapon.SlotId}: socket '{slot.Socket}' does not resolve to a bone");
                continue;
            }

            plan.Add(new PlannedPart
            {
                Kind = "weapon",
                Asset = meshPath,
                Slot = slot.SlotId,
                Socket = slot.Socket,
                Bone = bone,
                Tag = weaponInfo.Tag,
                Transform = boneMatrix
            });
        }
    }

    private static string? ResolveVisual(
        HardpointSlot slot,
        WeaponInfo weapon,
        string slotId,
        List<string> unmapped,
        List<string> warnings)
    {
        if (weapon.Tag is not null)
        {
            if (slot.Visuals.TryGetValue(weapon.Tag, out var exact))
            {
                return exact;
            }

            // MW5 tags stack more qualifiers than the visual map keys (…LRM20.Stream vs …LRM20).
            var closest = slot.Visuals.Keys
                .Where(key => key.StartsWith(weapon.Tag, StringComparison.OrdinalIgnoreCase) ||
                              weapon.Tag.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(key => key.Length)
                .FirstOrDefault();

            if (closest is not null)
            {
                return slot.Visuals[closest];
            }
        }

        if (slot.Visuals.TryGetValue("Weapon", out var fallback))
        {
            warnings.Add($"{slotId}: no visual for tag '{weapon.Tag}', using the slot's generic visual");
            return fallback;
        }

        unmapped.Add($"{slotId}: hardpoint slot has no visual for '{weapon.Tag}'");
        return null;
    }

    /// <summary><c>/Game/…/Weapon_Mech_X.Weapon_Mech_X_C</c> → <c>MW5Mercs/Content/…/Weapon_Mech_X</c>.</summary>
    internal static string GamePathToVirtual(string assetPath)
    {
        var path = assetPath.StartsWith("/Game/", StringComparison.OrdinalIgnoreCase)
            ? GameContentPrefix + assetPath["/Game/".Length..]
            : assetPath;

        var dot = path.LastIndexOf('.');
        return dot > 0 ? path[..dot] : path;
    }

    internal static string VisualToMeshPath(string assetPath) => GamePathToVirtual(assetPath) + "_SKM";

    private static async Task<Matrix4x4> ReadCockpitLocalAsync(
        UnrealArchive archive,
        string cockpitMesh,
        string dataDirectory,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var blueprint = cockpitMesh.Replace("_SKM", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (archive.FindFile(blueprint) is null)
        {
            warnings.Add($"cockpit blueprint '{blueprint}' not found; cockpit placed at its bone without offset");
            return Matrix4x4.Identity;
        }

        var blueprintDirectory = Path.Combine(dataDirectory, "cockpit");
        var export = await AssetExtractor.ExportAsync(
            archive,
            new ExtractRequest { AssetPath = blueprint, Format = ExtractFormat.Json, OutputDirectory = blueprintDirectory },
            cancellationToken);

        var attachment = UnrealDataReader.ReadCockpit(
            Path.GetDirectoryName(export.Files[0])!,
            Path.GetFileNameWithoutExtension(cockpitMesh));

        if (!attachment.Found)
        {
            warnings.Add("cockpit blueprint has no matching SkeletalMeshComponent; using identity offset");
            return Matrix4x4.Identity;
        }

        var local = Matrix4x4.CreateScale(attachment.Scale) *
                    Matrix4x4.CreateTranslation(attachment.TranslationCm / 100f);
        return UeToGltf * local * Matrix4x4.Transpose(UeToGltf);
    }

    private static Matrix4x4 Bone(
        IReadOnlyDictionary<string, Matrix4x4> boneMatrices,
        string boneName,
        List<string> warnings)
    {
        if (boneMatrices.TryGetValue(boneName, out var matrix))
        {
            return matrix;
        }

        warnings.Add($"bone '{boneName}' not found; part placed at the origin");
        return Matrix4x4.Identity;
    }

    private static IReadOnlyDictionary<string, string> ReadSocketBones(UnrealArchive archive, string bodyAsset)
    {
        var body = AssetInspector.Inspect(archive, bodyAsset);
        var skeletonName = body.Mesh?.SkeletonAsset;
        var skeleton = skeletonName is null ? null : archive.FindFile(skeletonName);
        if (skeleton is null)
        {
            return new Dictionary<string, string>();
        }

        var report = AssetInspector.Inspect(archive, skeleton.PathWithoutExtension);
        return (report.Skeleton?.Sockets ?? [])
            .ToDictionary(socket => socket.Name, socket => socket.Bone, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, Matrix4x4> ReadBoneMatrices(string glbPath)
    {
        var model = ModelRoot.Load(glbPath);
        var matrices = new Dictionary<string, Matrix4x4>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in model.LogicalNodes)
        {
            if (!string.IsNullOrWhiteSpace(node.Name))
            {
                matrices.TryAdd(node.Name, node.WorldMatrix);
            }
        }

        return matrices;
    }

    private static async Task<string> ExportMesh(
        UnrealArchive archive,
        string assetPath,
        string directory,
        CancellationToken cancellationToken)
    {
        var result = await AssetExtractor.ExportAsync(
            archive,
            new ExtractRequest { AssetPath = assetPath, Format = ExtractFormat.Gltf, OutputDirectory = directory },
            cancellationToken);

        return result.Files.First(file => file.EndsWith(".glb", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<string> ExportJson(
        UnrealArchive archive,
        string assetPath,
        string directory,
        CancellationToken cancellationToken)
    {
        var result = await AssetExtractor.ExportAsync(
            archive,
            new ExtractRequest { AssetPath = assetPath, Format = ExtractFormat.Json, OutputDirectory = directory },
            cancellationToken);

        return result.Files.First(file => file.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    }

    private static bool SameHardpoint(string left, string right) =>
        Equals(HardpointPrefix(left), HardpointPrefix(right));

    /// <summary><c>Torso_Left_MH1_lrm20</c> → <c>Torso_Left_MH1</c>.</summary>
    private static string HardpointPrefix(string slotId)
    {
        var tokens = slotId.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < tokens.Length; index++)
        {
            if (tokens[index].Length is 3 or 4 &&
                tokens[index][^1] is >= '0' and <= '9' &&
                tokens[index][..2] is "AH" or "EH" or "BH" or "MH" or "EB")
            {
                return string.Join('_', tokens.Take(index + 1));
            }
        }

        return slotId;
    }

    private static ChassisEntry SelectChassis(IReadOnlyList<ChassisEntry> chassis, string filter) =>
        chassis.FirstOrDefault(entry => Equals(entry.Folder, filter))
        ?? chassis.FirstOrDefault(entry => entry.Variants.Any(variant => Equals(variant.Id, filter)))
        ?? (chassis.Count == 1
            ? chassis[0]
            : throw new InvalidOperationException($"No chassis matches '{filter}'."));

    private static VariantEntry? SelectVariant(ChassisEntry chassis, string? variant) =>
        variant is null
            ? chassis.Variants.FirstOrDefault(candidate => candidate.Loadout is not null) ?? chassis.Variants.FirstOrDefault()
            : chassis.Variants.FirstOrDefault(candidate => Equals(candidate.Id, variant));

    private static bool Equals(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private sealed record PlannedPart
    {
        public required string Kind { get; init; }
        public required string Asset { get; init; }
        public string? Slot { get; init; }
        public string? Socket { get; init; }
        public string? Bone { get; init; }
        public string? Tag { get; init; }
        public required Matrix4x4 Transform { get; init; }
    }
}
