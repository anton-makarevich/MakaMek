using Sanet.MakaMek.Assets.Models.Terrains;

namespace Sanet.MakaMek.Assets.Services.PackageReaders;

/// <summary>
/// A single parsed terrain asset (image) from an MMTX package
/// </summary>
public sealed record TerrainAssetEntry(TerrainAssetType AssetType, string AssetName, int Variant, byte[] Image);