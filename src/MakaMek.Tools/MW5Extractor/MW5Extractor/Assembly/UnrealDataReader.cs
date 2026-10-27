using System.Numerics;
using System.Text.Json;

namespace MakaMek.Tools.MW5Extractor.Assembly;

/// <summary>A hardpoint slot from <c>&lt;Chassis&gt;_HPS</c>.</summary>
public sealed record HardpointSlot
{
    public required string SlotId { get; init; }
    public string? Socket { get; init; }
    public string? SlotType { get; init; }

    /// <summary>Weapon tag (<c>Weapon.…</c>) → visual Blueprint asset path.</summary>
    public required IReadOnlyDictionary<string, string> Visuals { get; init; }
}

/// <summary>One entry of <c>&lt;Model&gt;_Loadout</c>.</summary>
public sealed record InstalledWeapon
{
    public required string SlotId { get; init; }
    public string? WeaponAsset { get; init; }
}

/// <summary>The bits of a weapon data asset the assembly needs.</summary>
public sealed record WeaponInfo
{
    public string? Name { get; init; }
    public string? Tag { get; init; }
    public string? SlotType { get; init; }
}

/// <summary>How a cockpit Blueprint places its mesh inside the chassis.</summary>
public sealed record CockpitAttachment
{
    public string? Mesh { get; init; }

    /// <summary>Relative location in UE centimetres (Z-up).</summary>
    public Vector3 TranslationCm { get; init; }

    public Vector3 Scale { get; init; } = Vector3.One;
    public bool Found { get; init; }
}

/// <summary>The visible (third-person) cockpit: a static mesh on a chassis bone, declared by <c>&lt;Chassis&gt;_MDL</c>.</summary>
public sealed record ThirdPersonCockpit
{
    public string? MeshAssetPath { get; init; }
    public string BoneName { get; init; } = "Cockpit";
    public bool Found { get; init; }
}

/// <summary>
/// Reads the JSON that <c>extract --format json</c> produces for MW5's custom data assets.
/// CUE4Parse cannot type these classes, but the tagged properties survive as plain JSON.
/// </summary>
public static class UnrealDataReader
{
    public static IReadOnlyList<HardpointSlot> ReadHardpoints(string jsonFile)
    {
        using var document = Open(jsonFile);
        var slots = Property(document, "MechHardpoints", "WeaponSlots");
        if (slots is not { ValueKind: JsonValueKind.Array })
        {
            return [];
        }

        return slots.Value.EnumerateArray().Select(slot => new HardpointSlot
        {
            SlotId = String(slot, "HardpointSlotID") ?? string.Empty,
            Socket = String(slot, "MechSkeletonSocketID"),
            SlotType = NestedString(slot, "SlotTypeAssetId", "Id", "PrimaryAssetName"),
            Visuals = ReadVisuals(slot)
        }).ToList();
    }

    public static IReadOnlyList<InstalledWeapon> ReadLoadout(string jsonFile)
    {
        using var document = Open(jsonFile);
        var installed = Property(document, "MechLoadout", "InstalledWeapons");
        if (installed is not { ValueKind: JsonValueKind.Array })
        {
            return [];
        }

        return installed.Value.EnumerateArray().Select(weapon => new InstalledWeapon
        {
            SlotId = String(weapon, "HardpointSlotID") ?? string.Empty,
            WeaponAsset = NestedString(weapon, "WeaponData", "WeaponId", "Id", "PrimaryAssetName")
        }).ToList();
    }

    public static WeaponInfo ReadWeapon(string jsonFile)
    {
        using var document = Open(jsonFile);
        var root = document.RootElement;
        return new WeaponInfo
        {
            Name = String(root, "Name"),
            Tag = NestedString(root, "Properties", "WeaponType", "TagName"),
            SlotType = NestedString(root, "Properties", "WeaponSlotTypeAssetId", "Id", "PrimaryAssetName")
        };
    }

    /// <summary>
    /// The visible head: MW5 renders the first-person cockpit only from inside; the outside is a static mesh
    /// referenced from the chassis' model data asset and parented to a bone.
    /// </summary>
    public static ThirdPersonCockpit ReadThirdPersonCockpit(string modelJsonFile)
    {
        using var document = Open(modelJsonFile);
        var data = Nested(document.RootElement, "Properties", "MechModelData", "MechDestructibleState", "ThirdPersonCockpitData");
        if (data is not { ValueKind: JsonValueKind.Object } cockpit)
        {
            return new ThirdPersonCockpit();
        }

        return new ThirdPersonCockpit
        {
            MeshAssetPath = NestedString(cockpit, "InitialStaticMesh", "AssetPathName"),
            BoneName = String(cockpit, "BoneName") ?? "Cockpit",
            Found = true
        };
    }

    public static CockpitAttachment ReadCockpit(string exportDirectory, string meshName)
    {
        if (!Directory.Exists(exportDirectory))
        {
            return new CockpitAttachment();
        }

        foreach (var file in Directory.GetFiles(exportDirectory, "*.json", SearchOption.AllDirectories))
        {
            using var document = Open(file);
            var root = document.RootElement;
            if (String(root, "Type") != "SkeletalMeshComponent")
            {
                continue;
            }

            var mesh = NestedString(root, "Properties", "SkeletalMesh", "ObjectName");
            if (mesh is null || !mesh.Contains(meshName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return new CockpitAttachment
            {
                Mesh = mesh,
                TranslationCm = Vector(root, "Properties", "RelativeLocation"),
                Scale = VectorOr(root, Vector3.One, "Properties", "RelativeScale3D"),
                Found = true
            };
        }

        return new CockpitAttachment();
    }

    private static JsonDocument Open(string file) =>
        JsonDocument.Parse(File.ReadAllText(file), new JsonDocumentOptions { AllowTrailingCommas = true });

    private static Dictionary<string, string> ReadVisuals(JsonElement slot)
    {
        var visuals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!slot.TryGetProperty("HardpointVisuals", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return visuals;
        }

        foreach (var entry in array.EnumerateArray())
        {
            var tag = NestedString(entry, "Key", "TagName");
            var asset = NestedString(entry, "Value", "AssetPathName");
            if (!string.IsNullOrWhiteSpace(tag) && !string.IsNullOrWhiteSpace(asset) && asset != "None")
            {
                visuals[tag] = asset;
            }
        }

        return visuals;
    }

    private static JsonElement? Property(JsonDocument document, params string[] path) =>
        Nested(document.RootElement, ["Properties", ..path]);

    private static JsonElement? Nested(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? NestedString(JsonElement element, params string[] path) =>
        Nested(element, path) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static Vector3 Vector(JsonElement element, params string[] path) =>
        VectorOr(element, Vector3.Zero, path);

    private static Vector3 VectorOr(JsonElement element, Vector3 fallback, params string[] path) =>
        Nested(element, path) is { ValueKind: JsonValueKind.Object } value
            ? new Vector3(Number(value, "X"), Number(value, "Y"), Number(value, "Z"))
            : fallback;

    private static float Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetSingle()
            : 0f;
}
