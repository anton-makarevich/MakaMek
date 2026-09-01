using Sanet.MakaMek.Assets.Models.Terrains;

namespace Sanet.MakaMek.Assets.Services.PackageReaders;

/// <summary>
/// Parsed contents of an MMTX terrain package: the biome manifest and all extracted assets
/// </summary>
public sealed record TerrainPackage(BiomeManifest Manifest, IReadOnlyList<TerrainAssetEntry> Assets);