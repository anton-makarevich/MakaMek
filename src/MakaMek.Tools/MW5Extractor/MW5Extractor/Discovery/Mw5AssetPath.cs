using System.Text.RegularExpressions;

namespace MakaMek.Tools.MW5Extractor.Discovery;

/// <summary>
/// A classified MechWarrior 5 asset path.
/// </summary>
/// <remarks>
/// Discovery is deliberately a path problem, not an asset-graph problem: MW5 filenames encode chassis, location,
/// hardpoint and weapon (<c>Weapon_Mech_AS7_Torso_Right_BH1_AC20_SKM</c>), and enumerating pak indexes is cheap
/// where deserialising cooked assets is not. The custom data classes behind hardpoint sets and loadouts are
/// untagged and unreadable through CUE4Parse, so filenames stay the source of truth for discovery.
/// </remarks>
public sealed record Mw5AssetPath
{
    private static readonly string[] VariantSuffixes = ["_Loadout", "_MDA", "_UnitCard"];

    /// <summary>Folder names that only ever appear *inside* a chassis folder.</summary>
    private static readonly string[] ChildFolders =
    [
        "Model", "MDA", "Loadout", "Animation", "Animations", "Skins", "Footprints",
        "Target", "UnitCard", "Audio", "Textures", "Materials", "Particles"
    ];

    /// <summary>Full virtual path, e.g. <c>MW5Mercs/Content/Objects/Mechs/Atlas/Model/Body/Atlas_SKM.uasset</c>.</summary>
    public required string VirtualPath { get; init; }

    public required string FileName { get; init; }

    public required Mw5AssetKind Kind { get; init; }

    /// <summary>Content root, e.g. <c>MW5Mercs/Content</c> or <c>DLC3/Content</c>.</summary>
    public string? ContentRoot { get; init; }

    /// <summary>Mech folder under <c>…/Objects/Mechs/</c>, e.g. <c>Atlas</c>.</summary>
    public string? ChassisFolder { get; init; }

    /// <summary>Variant id recovered from a loadout / data asset name, e.g. <c>AS7-D</c>.</summary>
    public string? Variant { get; init; }

    /// <summary>Parsed weapon slot for <see cref="Mw5AssetKind.Weapon"/> entries.</summary>
    public WeaponSlot? Weapon { get; init; }

    public string Name => FileName[..^".uasset".Length];

    public bool IsMechAsset => ChassisFolder is not null;

    /// <summary>Classifies a virtual path. Returns <c>null</c> for files that are not cooked packages.</summary>
    public static Mw5AssetPath? TryParse(string virtualPath)
    {
        var path = virtualPath.Replace('\\', '/');
        var slash = path.LastIndexOf('/');
        var fileName = slash >= 0 ? path[(slash + 1)..] : path;
        if (!fileName.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = fileName[..^".uasset".Length];
        var segments = path.Split('/');
        var mechsIndex = Array.FindIndex(segments, s => s.Equals("Mechs", StringComparison.OrdinalIgnoreCase));

        var (chassisFolder, chassisIndex) = ResolveChassisFolder(segments, mechsIndex);
        var contentRoot = mechsIndex > 1
            ? string.Join('/', segments.Take(mechsIndex - 1))
            : Reporting.MountReportBuilder.ContentRootOf(path);

        var relative = chassisIndex >= 0 && chassisIndex + 1 < segments.Length - 1
            ? string.Join('/', segments[(chassisIndex + 1)..^1])
            : string.Empty;

        var classified = Classify(chassisFolder, relative, name);
        return new Mw5AssetPath
        {
            VirtualPath = path,
            FileName = fileName,
            Kind = classified.Kind,
            ContentRoot = contentRoot,
            ChassisFolder = chassisFolder,
            Variant = classified.Variant,
            Weapon = classified.Weapon
        };
    }

    /// <summary>
    /// Finds the chassis folder: the segment in front of the first known child folder (<c>Model</c>, <c>Loadout</c>, …).
    /// Clan mechs live one level deeper (<c>Mechs/CLANS/&lt;Chassis&gt;/…</c>), so "the segment after Mechs" is not enough.
    /// </summary>
    private static (string? Folder, int Index) ResolveChassisFolder(string[] segments, int mechsIndex)
    {
        if (mechsIndex < 0)
        {
            return (null, -1);
        }

        var first = mechsIndex + 1;
        var lastFolder = segments.Length - 2;
        if (first > lastFolder)
        {
            return (null, -1);
        }

        for (var index = first; index <= lastFolder; index++)
        {
            if (index == first)
            {
                // A "child folder" name directly under Mechs is a shared container, not a chassis folder.
                continue;
            }

            if (ChildFolders.Any(folder => folder.Equals(segments[index], StringComparison.OrdinalIgnoreCase)))
            {
                return (segments[index - 1], index - 1);
            }
        }

        return (segments[lastFolder], lastFolder);
    }

    private static (Mw5AssetKind Kind, string? Variant, WeaponSlot? Weapon) Classify(
        string? chassisFolder,
        string relative,
        string name)
    {
        if (chassisFolder is null)
        {
            return (Mw5AssetKind.Other, null, null);
        }

        // `relative` is the folder path below the chassis folder (no file name); give it a trailing separator
        // so prefix tests read like the paths they match.
        var tree = relative.Length == 0 ? "/" : relative + "/";

        // Variants sometimes sit directly in the chassis folder (DLC8 stores AtlasC_Loadout/_MDA there).
        if (!relative.Contains('/'))
        {
            if (name.EndsWith("_Loadout", StringComparison.OrdinalIgnoreCase))
            {
                return (Mw5AssetKind.Loadout, StripVariantSuffix(name), null);
            }

            if (name.EndsWith("_MDA", StringComparison.OrdinalIgnoreCase))
            {
                return (Mw5AssetKind.MechData, StripVariantSuffix(name), null);
            }

            if (name.EndsWith("_UnitCard", StringComparison.OrdinalIgnoreCase))
            {
                return (Mw5AssetKind.UnitCard, StripVariantSuffix(name), null);
            }
        }

        if (IsUnder(tree, "Model/Body/"))
        {
            // Materials and textures live in the same subtree but are not geometry.
            if (IsUnder(tree, "Model/Body/Materials/") || IsUnder(tree, "Model/Body/Textures/"))
            {
                return (Mw5AssetKind.Other, null, null);
            }

            if (name.Contains("Physics", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Ragdoll", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("_PHY", StringComparison.OrdinalIgnoreCase))
            {
                return (Mw5AssetKind.Other, null, null);
            }

            if (IsDamageState(name))
            {
                return (Mw5AssetKind.BodyDamage, null, null);
            }

            // Bodies (monolithic <Chassis>_SKM, or a modular chassis' <ModelCode>) sit directly in Model/Body.
            return (relative.Equals("Model/Body", StringComparison.OrdinalIgnoreCase)
                ? Mw5AssetKind.BodyMesh
                : Mw5AssetKind.BodyPart, null, null);
        }

        if (IsUnder(tree, "Model/Cockpit/"))
        {
            if (name.Contains("glass", StringComparison.OrdinalIgnoreCase))
            {
                return (Mw5AssetKind.CockpitGlass, null, null);
            }

            return name.EndsWith("_SKM", StringComparison.OrdinalIgnoreCase)
                ? (Mw5AssetKind.Cockpit, null, null)
                : (Mw5AssetKind.Other, null, null);
        }

        if (IsUnder(tree, "Model/Weapons/"))
        {
            return (Mw5AssetKind.Weapon, null, WeaponSlot.TryParse(name));
        }

        if (IsUnder(tree, "Model/"))
        {
            // Only assets sitting at the Model root are the chassis' own definitions.
            var modelRootDepth = relative.Count(c => c == '/') == 0;
            if (modelRootDepth)
            {
                if (name.EndsWith("_HPS", StringComparison.OrdinalIgnoreCase))
                {
                    return (Mw5AssetKind.HardpointSet, null, null);
                }

                if (name.EndsWith("_MDL", StringComparison.OrdinalIgnoreCase))
                {
                    return (Mw5AssetKind.ModelDefinition, null, null);
                }
            }

            return (Mw5AssetKind.Other, null, null);
        }

        if (IsUnder(tree, "MDA/"))
        {
            return (Mw5AssetKind.MechData, StripVariantSuffix(name), null);
        }

        if (IsUnder(tree, "Loadout/"))
        {
            return (Mw5AssetKind.Loadout, StripVariantSuffix(name), null);
        }

        if (IsUnder(tree, "Animation/") || IsUnder(tree, "Animations/"))
        {
            return (Mw5AssetKind.Animation, null, null);
        }

        if (IsUnder(tree, "Skins/"))
        {
            return (Mw5AssetKind.Skin, null, null);
        }

        if (IsUnder(tree, "UnitCard/"))
        {
            return (Mw5AssetKind.UnitCard, StripVariantSuffix(name), null);
        }

        return (Mw5AssetKind.Other, null, null);
    }

    private static bool IsUnder(string relative, string folder) =>
        relative.StartsWith(folder, StringComparison.OrdinalIgnoreCase);

    private static bool IsDamageState(string name)
    {
        return name.Contains("_DM0", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("_DM1", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("_DM2", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("_Debris", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith("_STM", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripVariantSuffix(string name)
    {
        foreach (var suffix in VariantSuffixes)
        {
            var index = name.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                return name[..index];
            }
        }

        return name;
    }
}

/// <summary>A weapon hardpoint recovered from an asset name.</summary>
public sealed record WeaponSlot
{
    private static readonly Regex HardpointToken = new(
        @"^(AH|EH|BH|MH|EB)\d+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Chassis token in the asset name, e.g. <c>AS7</c> (MegaMek model code prefix).</summary>
    public required string ChassisToken { get; init; }

    /// <summary>Location token, e.g. <c>Torso_Right</c>, <c>Forearm_Left</c>, <c>Clavicle_Left</c>.</summary>
    public required string Location { get; init; }

    /// <summary>Hardpoint token, e.g. <c>BH1</c> (ballistic), <c>MH1</c> (missile), <c>EH1</c> (energy).</summary>
    public string? Hardpoint { get; init; }

    /// <summary>Weapon token, e.g. <c>AC20</c>, <c>Rl20</c>, <c>Laser</c>, <c>Blank</c>.</summary>
    public string? Weapon { get; init; }

    /// <summary>Asset suffix without the leading underscore: <c>SKM</c>, <c>PHY</c>, <c>ANI</c>, or empty.</summary>
    public string Suffix { get; init; } = string.Empty;

    public bool IsMesh => Suffix.Contains("SKM", StringComparison.OrdinalIgnoreCase);

    public static WeaponSlot? TryParse(string name)
    {
        const string prefix = "Weapon_Mech_";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var rest = name[prefix.Length..];
        var suffixParts = new List<string>();
        var tokens = rest.Split('_', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Sidecar suffixes stack (e.g. ..._SKM_Physics); strip them from the end before parsing the slot.
        while (tokens.Count > 1 && IsMeshSuffix(tokens[^1]))
        {
            suffixParts.Insert(0, tokens[^1].ToUpperInvariant());
            tokens.RemoveAt(tokens.Count - 1);
        }

        var suffix = string.Join('_', suffixParts);
        if (tokens.Count == 0)
        {
            return null;
        }

        var hardpointIndex = tokens.FindIndex(token => HardpointToken.IsMatch(token));
        if (hardpointIndex < 1)
        {
            return new WeaponSlot
            {
                ChassisToken = tokens[0],
                Location = string.Join('_', tokens.Skip(1)),
                Suffix = suffix
            };
        }

        return new WeaponSlot
        {
            ChassisToken = tokens[0],
            Location = string.Join('_', tokens.Skip(1).Take(hardpointIndex - 1)),
            Hardpoint = tokens[hardpointIndex],
            Weapon = string.Join('_', tokens.Skip(hardpointIndex + 1)),
            Suffix = suffix
        };
    }

    private static bool IsMeshSuffix(string token) =>
        token.Equals("SKM", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("PHY", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("Physics", StringComparison.OrdinalIgnoreCase) ||
        token.Equals("ANI", StringComparison.OrdinalIgnoreCase);
}
