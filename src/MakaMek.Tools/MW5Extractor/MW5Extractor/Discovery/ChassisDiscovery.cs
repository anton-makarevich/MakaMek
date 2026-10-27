using MakaMek.Tools.MW5Extractor.Reporting;

namespace MakaMek.Tools.MW5Extractor.Discovery;

/// <summary>Builds a <see cref="DiscoveryReport"/> from a mounted pak set without deserialising anything.</summary>
public static class ChassisDiscovery
{
    private const string BodySuffix = "_SKM";
    private const string CockpitSuffix = "_Cockpit_SKM";
    private const string HardpointSetSuffix = "_HPS";
    private const string ModelDefinitionSuffix = "_MDL";

    public static DiscoveryReport Build(UnrealArchive archive, string? chassisFilter = null, DateTimeOffset? generatedAt = null)
    {
        var assets = new List<Mw5AssetPath>();
        foreach (var file in archive.Files)
        {
            var parsed = Mw5AssetPath.TryParse(file.Path);
            if (parsed is { IsMechAsset: true })
            {
                assets.Add(parsed);
            }
        }

        var entries = assets
            .GroupBy(asset => asset.ChassisFolder!, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildEntry(group.Key, group.ToList()))
            .OrderBy(entry => entry.Folder, StringComparer.Ordinal)
            .ToList();

        var allChassis = entries.Where(IsChassis).ToList();
        var chassisFolders = new HashSet<string>(allChassis.Select(entry => entry.Folder), StringComparer.OrdinalIgnoreCase);
        var otherFolders = assets
            .Where(asset => !chassisFolders.Contains(asset.ChassisFolder!))
            .GroupBy(asset => asset.ChassisFolder!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OtherFolderEntry
            {
                Folder = group.Key,
                AssetCount = group.Count(),
                Kinds = group.Select(asset => asset.Kind.ToString())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(kind => kind, StringComparer.Ordinal)
                    .ToList()
            })
            .OrderBy(entry => entry.Folder, StringComparer.Ordinal)
            .ToList();

        var chassis = allChassis;
        var warnings = new List<string>(archive.Warnings);
        if (!string.IsNullOrWhiteSpace(chassisFilter))
        {
            chassis = chassis.Where(entry => MatchesFilter(entry, chassisFilter)).ToList();
            warnings.Add($"Filtered to '{chassisFilter}': {chassis.Count} of {allChassis.Count} chassis reported.");

            if (chassis.Count == 0)
            {
                warnings.Add($"No chassis matches '{chassisFilter}'; known folders: {string.Join(", ", allChassis.Select(e => e.Folder).Take(10))}…");
            }
        }

        return new DiscoveryReport
        {
            GeneratedAt = generatedAt ?? DateTimeOffset.UtcNow,
            Source = new DiscoverySource
            {
                PaksDirectory = archive.Options.PaksDirectory,
                Game = archive.Options.Game.ToString(),
                FileCount = archive.FileCount,
                Archives = archive.Archives.Select(reader => reader.Name).OrderBy(name => name, StringComparer.Ordinal).ToList()
            },
            Summary = Summarize(chassis),
            Chassis = chassis,
            OtherFolders = otherFolders,
            AssumptionChecks = CheckAssumptions(assets, allChassis, otherFolders),
            Warnings = warnings
        };
    }

    private static ChassisEntry BuildEntry(string folder, List<Mw5AssetPath> assets)
    {
        var warnings = new List<string>();

        // Weapon names carry the chassis' model code (AS7, AS7II, …); the body mesh usually carries the same code.
        var chassisToken = assets
            .Select(asset => asset.Weapon?.ChassisToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .GroupBy(token => token!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .FirstOrDefault();

        var bodyCandidates = assets
            .Where(asset => asset.Kind == Mw5AssetKind.BodyMesh)
            .OrderBy(asset => asset.VirtualPath, StringComparer.Ordinal)
            .ToList();

        var bodyAsset = bodyCandidates.FirstOrDefault(asset => Equals(asset.Name, folder + BodySuffix))
                        ?? bodyCandidates.FirstOrDefault(asset => chassisToken is not null && Equals(asset.Name, chassisToken))
                        ?? bodyCandidates.FirstOrDefault();

        var bodyLayout = bodyAsset is null
            ? BodyLayoutKind.Missing
            : Equals(bodyAsset.Name, folder + BodySuffix)
                ? BodyLayoutKind.Monolithic
                : BodyLayoutKind.Modular;

        if (bodyAsset is null)
        {
            warnings.Add("no body mesh under Model/Body");
        }
        else if (bodyLayout == BodyLayoutKind.Modular)
        {
            warnings.Add(
                $"modular body: no {folder}{BodySuffix}; using {bodyAsset.Name} as the body " +
                "(the chassis is assembled from part meshes, not one skeletal mesh)");
        }

        if (bodyCandidates.Count > 1)
        {
            warnings.Add(
                $"multiple body candidates; using {bodyAsset?.VirtualPath} " +
                $"(others: {string.Join(", ", bodyCandidates.Where(asset => asset != bodyAsset).Select(asset => asset.Name))})");
        }

        var cockpit = Single(assets, Mw5AssetKind.Cockpit, name => Equals(name, folder + CockpitSuffix), "cockpit", warnings);
        var glass = Single(assets, Mw5AssetKind.CockpitGlass, name => StartsWith(name, folder), "cockpit glass", warnings);
        var hardpointSet = Single(assets, Mw5AssetKind.HardpointSet, name => Equals(name, folder + HardpointSetSuffix), "hardpoint set", warnings);
        var modelDefinition = Single(assets, Mw5AssetKind.ModelDefinition, name => Equals(name, folder + ModelDefinitionSuffix), "model definition", warnings);

        if (bodyAsset is not null && cockpit is null)
        {
            warnings.Add($"no cockpit mesh ({folder}_Cockpit_SKM)");
        }

        if (cockpit is not null && glass is null)
        {
            warnings.Add("cockpit without cockpit glass");
        }

        var weapons = assets
            .Where(asset => asset.Kind == Mw5AssetKind.Weapon)
            .OrderBy(asset => asset.VirtualPath, StringComparer.Ordinal)
            .Select(asset => new WeaponEntry
            {
                Path = asset.VirtualPath,
                Location = asset.Weapon?.Location,
                Hardpoint = asset.Weapon?.Hardpoint,
                Weapon = asset.Weapon?.Weapon,
                Suffix = asset.Weapon?.Suffix ?? string.Empty
            })
            .ToList();

        if (weapons.Any(weapon => weapon.Suffix.Equals("SKM", StringComparison.OrdinalIgnoreCase)) && hardpointSet is null)
        {
            warnings.Add($"weapon meshes present but no hardpoint set ({folder}_HPS)");
        }

        if (bodyAsset is null && chassisToken is not null && !Equals(chassisToken, folder))
        {
            warnings.Add(
                $"no body in this folder and its weapon assets carry the '{chassisToken}' code — " +
                "the chassis likely reuses another chassis' meshes");
        }

        var bodyParts = assets
            .Where(asset => asset.Kind == Mw5AssetKind.BodyPart)
            .Select(asset => asset.VirtualPath)
            .Concat(bodyCandidates.Where(asset => asset != bodyAsset).Select(asset => asset.VirtualPath))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var variants = assets
            .Where(asset => asset.Variant is not null)
            .GroupBy(asset => asset.Variant!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new VariantEntry
            {
                Id = group.Key,
                MechData = group.Where(a => a.Kind == Mw5AssetKind.MechData)
                    .Select(a => a.VirtualPath).OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault(),
                Loadout = group.Where(a => a.Kind == Mw5AssetKind.Loadout)
                    .Select(a => a.VirtualPath).OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault(),
                UnitCard = group.Where(a => a.Kind == Mw5AssetKind.UnitCard)
                    .Select(a => a.VirtualPath).OrderBy(p => p, StringComparer.Ordinal).FirstOrDefault()
            })
            .ToList();

        foreach (var variant in variants.Where(v => v.MechData is null || v.Loadout is null))
        {
            warnings.Add($"variant '{variant.Id}' has {(variant.MechData is null ? "no data asset" : "no loadout")}");
        }

        var animations = assets
            .Where(asset => asset.Kind == Mw5AssetKind.Animation)
            .Select(asset => asset.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return new ChassisEntry
        {
            Folder = folder,
            ContentRoots = assets.Select(asset => asset.ContentRoot!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(root => root, StringComparer.Ordinal)
                .ToList(),
            Body = bodyAsset?.VirtualPath,
            BodyLayout = bodyLayout,
            BodyParts = bodyParts,
            Cockpit = cockpit,
            CockpitGlass = glass,
            HardpointSet = hardpointSet,
            ModelDefinition = modelDefinition,
            Weapons = weapons,
            Variants = variants,
            Animations = animations,
            Counts = new ChassisCounts
            {
                BodyParts = bodyParts.Count,
                BodyDamage = assets.Count(asset => asset.Kind == Mw5AssetKind.BodyDamage),
                Weapons = weapons.Count,
                WeaponMeshes = weapons.Count(weapon => weapon.Suffix.Equals("SKM", StringComparison.OrdinalIgnoreCase)),
                Animations = animations.Count,
                Variants = variants.Count,
                Skins = assets.Count(asset => asset.Kind == Mw5AssetKind.Skin),
                Other = assets.Count(asset => asset.Kind == Mw5AssetKind.Other)
            },
            Warnings = warnings
        };
    }

    /// <summary>A folder is a chassis when it has body geometry, or weapons plus a hardpoint set.</summary>
    private static bool IsChassis(ChassisEntry entry) =>
        entry.Body is not null || (entry.HardpointSet is not null && entry.Counts.WeaponMeshes > 0);

    private static DiscoverySummary Summarize(IReadOnlyList<ChassisEntry> chassis)
    {
        return new DiscoverySummary
        {
            ChassisCount = chassis.Count,
            WithBody = chassis.Count(entry => entry.Body is not null),
            WithCockpit = chassis.Count(entry => entry.Cockpit is not null),
            WithCockpitGlass = chassis.Count(entry => entry.CockpitGlass is not null),
            WithWeapons = chassis.Count(entry => entry.Counts.WeaponMeshes > 0),
            WithHardpointSet = chassis.Count(entry => entry.HardpointSet is not null),
            WithVariants = chassis.Count(entry => entry.Variants.Count > 0),
            ModularBody = chassis.Count(entry => entry.BodyLayout == BodyLayoutKind.Modular),
            VariantCount = chassis.Sum(entry => entry.Variants.Count),
            WeaponMeshCount = chassis.Sum(entry => entry.Counts.WeaponMeshes),
            AnimationCount = chassis.Sum(entry => entry.Counts.Animations),
            ChassisWithoutCockpit = chassis.Where(entry => entry.Cockpit is null).Select(entry => entry.Folder).ToList(),
            ChassisWithoutCockpitGlass = chassis.Where(entry => entry.CockpitGlass is null).Select(entry => entry.Folder).ToList(),
            ChassisWithoutWeapons = chassis.Where(entry => entry.Counts.WeaponMeshes == 0).Select(entry => entry.Folder).ToList(),
            ChassisWithoutHardpointSet = chassis.Where(entry => entry.HardpointSet is null).Select(entry => entry.Folder).ToList(),
            ChassisWithoutVariants = chassis.Where(entry => entry.Variants.Count == 0).Select(entry => entry.Folder).ToList()
        };
    }

    private static IReadOnlyList<AssumptionCheck> CheckAssumptions(
        IReadOnlyList<Mw5AssetPath> assets,
        IReadOnlyList<ChassisEntry> chassis,
        IReadOnlyList<OtherFolderEntry> otherFolders)
    {
        var monolithic = chassis.Where(entry => entry.BodyLayout == BodyLayoutKind.Monolithic).ToList();
        var modular = chassis.Where(entry => entry.BodyLayout == BodyLayoutKind.Modular).ToList();
        var variants = chassis.Sum(entry => entry.Variants.Count);
        var weaponMeshes = chassis.Sum(entry => entry.Counts.WeaponMeshes);
        var chassisWithWeapons = chassis.Count(entry => entry.Counts.WeaponMeshes > 0);
        var glassCount = chassis.Count(entry => entry.CockpitGlass is not null);
        var hpsCount = chassis.Count(entry => entry.HardpointSet is not null);
        var duplicatePaths = CountCrossRootDuplicates(assets);

        return
        [
            new AssumptionCheck
            {
                Assumption = "One skeletal mesh per chassis at Model/Body/<Chassis>_SKM",
                Status = modular.Count == 0
                    ? AssumptionStatus.Confirmed
                    : AssumptionStatus.Contradicted,
                Evidence = $"{monolithic.Count} chassis use <Chassis>_SKM; " +
                           $"{modular.Count} have no such mesh and are assembled from part meshes instead (e.g. " +
                           $"{string.Join(", ", modular.Take(6).Select(entry => entry.Folder))})"
            },
            new AssumptionCheck
            {
                Assumption = "Variants share the chassis body; only data assets differ",
                Status = variants > 0 ? AssumptionStatus.Confirmed : AssumptionStatus.Contradicted,
                Evidence = $"{variants} variant data/loadout assets over {chassis.Count} chassis; " +
                           $"{duplicatePaths} asset paths are repeated across content roots (base game vs DLC)"
            },
            new AssumptionCheck
            {
                Assumption = "Cockpit and cockpit glass are separate assets from the body",
                Status = glassCount == chassis.Count ? AssumptionStatus.Confirmed : AssumptionStatus.Partial,
                Evidence = $"{chassis.Count(entry => entry.Cockpit is not null)} cockpit meshes, {glassCount} cockpit glass meshes, " +
                           $"{chassis.Count - glassCount} chassis without glass"
            },
            new AssumptionCheck
            {
                Assumption = "Weapons are separate meshes under Model/Weapons, named with location and hardpoint",
                Status = weaponMeshes > 0 ? AssumptionStatus.Confirmed : AssumptionStatus.Contradicted,
                Evidence = $"{weaponMeshes} weapon meshes across {chassisWithWeapons} chassis"
            },
            new AssumptionCheck
            {
                Assumption = "Every chassis ships a hardpoint set (<Chassis>_HPS)",
                Status = hpsCount == chassis.Count ? AssumptionStatus.Confirmed : AssumptionStatus.Partial,
                Evidence = $"{hpsCount} of {chassis.Count} chassis have <Chassis>_HPS"
            },
            new AssumptionCheck
            {
                Assumption = "…/Objects/Mechs/<folder> is a chassis",
                Status = otherFolders.Count == 0 ? AssumptionStatus.Confirmed : AssumptionStatus.Partial,
                Evidence = $"{otherFolders.Count} non-chassis folders under Objects/Mechs, e.g. " +
                           string.Join(", ", otherFolders.Take(5).Select(folder => folder.Folder))
            }
        ];
    }

    private static int CountCrossRootDuplicates(IReadOnlyList<Mw5AssetPath> assets)
    {
        return assets
            .Where(asset => asset.ContentRoot is not null)
            .GroupBy(asset => asset.VirtualPath[(asset.ContentRoot!.Length + 1)..], StringComparer.OrdinalIgnoreCase)
            .Count(group => group.Select(asset => asset.ContentRoot).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1);
    }

    private static string? Single(
        IReadOnlyList<Mw5AssetPath> assets,
        Mw5AssetKind kind,
        Func<string, bool> preferred,
        string label,
        ICollection<string> warnings)
    {
        var candidates = assets
            .Where(asset => asset.Kind == kind)
            .OrderBy(asset => asset.VirtualPath, StringComparer.Ordinal)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0].VirtualPath;
        }

        var chosen = candidates.FirstOrDefault(asset => preferred(asset.Name)) ?? candidates[0];
        var others = candidates.Where(asset => asset != chosen).Select(asset => asset.VirtualPath);
        warnings.Add($"multiple {label} assets found; using {chosen.VirtualPath} (ignored: {string.Join(", ", others)})");
        return chosen.VirtualPath;
    }

    private static bool MatchesFilter(ChassisEntry entry, string filter)
    {
        return Equals(entry.Folder, filter) ||
               entry.Variants.Any(variant =>
                   Equals(variant.Id, filter) ||
                   variant.Id.StartsWith(filter, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Picks the chassis a filter refers to: a folder, a variant id, or - when the filter narrowed the report to one
    /// chassis - that chassis.
    /// </summary>
    public static ChassisEntry Select(IReadOnlyList<ChassisEntry> chassis, string filter) =>
        chassis.FirstOrDefault(entry => Equals(entry.Folder, filter))
        ?? chassis.FirstOrDefault(entry => entry.Variants.Any(variant => Equals(variant.Id, filter)))
        ?? (chassis.Count == 1
            ? chassis[0]
            : throw new InvalidOperationException($"No chassis matches '{filter}'."));

    private static bool Equals(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWith(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
