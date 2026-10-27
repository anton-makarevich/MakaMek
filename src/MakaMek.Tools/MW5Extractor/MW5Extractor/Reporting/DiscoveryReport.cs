namespace MakaMek.Tools.MW5Extractor.Reporting;

/// <summary>What a whole pak set looks like: which chassis exist and what art each one carries.</summary>
public sealed record DiscoveryReport
{
    public int Schema { get; init; } = 1;
    public required DateTimeOffset GeneratedAt { get; init; }
    public required DiscoverySource Source { get; init; }
    public required DiscoverySummary Summary { get; init; }
    public required IReadOnlyList<ChassisEntry> Chassis { get; init; }

    /// <summary>Folders under <c>…/Objects/Mechs/</c> that are not a chassis (no body mesh).</summary>
    public required IReadOnlyList<OtherFolderEntry> OtherFolders { get; init; }

    /// <summary>Assumptions taken from the research notes, checked against the pak rather than trusted.</summary>
    public required IReadOnlyList<AssumptionCheck> AssumptionChecks { get; init; }

    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed record DiscoverySource
{
    public required string PaksDirectory { get; init; }
    public required string Game { get; init; }
    public required int FileCount { get; init; }
    public required IReadOnlyList<string> Archives { get; init; }
}

public sealed record DiscoverySummary
{
    public required int ChassisCount { get; init; }
    public required int WithBody { get; init; }
    public required int WithCockpit { get; init; }
    public required int WithCockpitGlass { get; init; }
    public required int WithWeapons { get; init; }
    public required int WithHardpointSet { get; init; }
    public required int WithVariants { get; init; }
    public required int ModularBody { get; init; }
    public required int VariantCount { get; init; }
    public required int WeaponMeshCount { get; init; }
    public required int AnimationCount { get; init; }
    public required IReadOnlyList<string> ChassisWithoutCockpit { get; init; }
    public required IReadOnlyList<string> ChassisWithoutCockpitGlass { get; init; }
    public required IReadOnlyList<string> ChassisWithoutWeapons { get; init; }
    public required IReadOnlyList<string> ChassisWithoutHardpointSet { get; init; }
    public required IReadOnlyList<string> ChassisWithoutVariants { get; init; }
}

public sealed record ChassisEntry
{
    /// <summary>Folder under <c>…/Objects/Mechs/</c>, e.g. <c>Atlas</c>. Art identity, not the game's chassis code.</summary>
    public required string Folder { get; init; }

    /// <summary>Content roots the chassis' assets come from, e.g. <c>["MW5Mercs/Content", "DLC3/Content"]</c>.</summary>
    public required IReadOnlyList<string> ContentRoots { get; init; }

    public required string? Body { get; init; }
    public required BodyLayoutKind BodyLayout { get; init; }
    public required IReadOnlyList<string> BodyParts { get; init; }
    public required string? Cockpit { get; init; }
    public required string? CockpitGlass { get; init; }
    public required string? HardpointSet { get; init; }
    public required string? ModelDefinition { get; init; }
    public required IReadOnlyList<WeaponEntry> Weapons { get; init; }
    public required IReadOnlyList<VariantEntry> Variants { get; init; }

    /// <summary>Animation asset names (the chassis + clip are encoded in the name).</summary>
    public required IReadOnlyList<string> Animations { get; init; }

    public required ChassisCounts Counts { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed record WeaponEntry
{
    public required string Path { get; init; }
    public required string? Location { get; init; }
    public required string? Hardpoint { get; init; }
    public required string? Weapon { get; init; }
    public required string Suffix { get; init; }
}

public sealed record VariantEntry
{
    /// <summary>Variant id from the data asset name, e.g. <c>AS7-D</c>.</summary>
    public required string Id { get; init; }
    public string? MechData { get; init; }
    public string? Loadout { get; init; }
    public string? UnitCard { get; init; }
}

public sealed record ChassisCounts
{
    public required int BodyParts { get; init; }
    public required int BodyDamage { get; init; }
    public required int Weapons { get; init; }
    public required int WeaponMeshes { get; init; }
    public required int Animations { get; init; }
    public required int Variants { get; init; }
    public required int Skins { get; init; }
    public required int Other { get; init; }
}

/// <summary>How the chassis' body geometry is authored in MW5.</summary>
public enum BodyLayoutKind
{
    /// <summary>One skeletal mesh per chassis: <c>Model/Body/&lt;Chassis&gt;_SKM</c>.</summary>
    Monolithic,

    /// <summary>No single body mesh; the chassis is a set of part meshes (newer DLC chassis).</summary>
    Modular,

    /// <summary>No body geometry found at all.</summary>
    Missing
}

public sealed record OtherFolderEntry
{
    public required string Folder { get; init; }
    public required int AssetCount { get; init; }
    public required IReadOnlyList<string> Kinds { get; init; }
}

public enum AssumptionStatus
{
    Confirmed,
    Partial,
    Contradicted
}

public sealed record AssumptionCheck
{
    public required string Assumption { get; init; }
    public required AssumptionStatus Status { get; init; }
    public required string Evidence { get; init; }
}
