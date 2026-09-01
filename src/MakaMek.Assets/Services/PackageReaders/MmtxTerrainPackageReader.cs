using System.IO.Compression;
using System.Text.Json;
using Sanet.MakaMek.Assets.Models.Terrains;

namespace Sanet.MakaMek.Assets.Services.PackageReaders;

/// <summary>
/// A single parsed terrain asset (image) from an MMTX package
/// </summary>
public sealed record TerrainAssetEntry(TerrainAssetType AssetType, string AssetName, int Variant, byte[] Image);

/// <summary>
/// Parsed contents of an MMTX terrain package: the biome manifest and all extracted assets
/// </summary>
public sealed record TerrainPackage(BiomeManifest Manifest, IReadOnlyList<TerrainAssetEntry> Assets);

/// <summary>
/// Format-specific reader for MMTX terrain packages
/// (<c>manifest.json</c> + per-asset PNGs: base/overlay/edge/water/road with variant suffixes)
/// </summary>
public class MmtxTerrainPackageReader
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Reads an MMTX terrain package stream
    /// </summary>
    /// <param name="mmtxStream">Stream containing the MMTX package data</param>
    /// <returns>The parsed terrain package</returns>
    /// <exception cref="InvalidOperationException">Thrown when the package is missing required entries or data is invalid</exception>
    public async Task<TerrainPackage> ReadAsync(Stream mmtxStream)
    {
        await using var archive = new ZipArchive(mmtxStream, ZipArchiveMode.Read);

        // Load manifest.json
        var manifestEntry = archive.GetEntry("manifest.json");
        if (manifestEntry == null)
        {
            throw new InvalidOperationException("MMTX package missing manifest.json");
        }

        BiomeManifest manifest;
        await using (var manifestStream = await manifestEntry.OpenAsync())
        using (var reader = new StreamReader(manifestStream))
        {
            var jsonContent = await reader.ReadToEndAsync();
            manifest = JsonSerializer.Deserialize<BiomeManifest>(jsonContent, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize manifest.json");
        }

        if (string.IsNullOrEmpty(manifest.Id))
        {
            throw new InvalidOperationException("MMTX package manifest missing id");
        }

        var assets = new List<TerrainAssetEntry>();
        await ExtractImagesFromDirectoryAsync(archive, "", TerrainAssetType.Base, assets);
        await ExtractImagesFromDirectoryAsync(archive, "terrains/", TerrainAssetType.Overlay, assets);
        await ExtractImagesFromDirectoryAsync(archive, "terrains/water/", TerrainAssetType.Water, assets);
        await ExtractImagesFromDirectoryAsync(archive, "terrains/road/", TerrainAssetType.Road, assets);
        await ExtractEdgeImagesAsync(archive, assets);

        return new TerrainPackage(manifest, assets);
    }

    private async Task ExtractImagesFromDirectoryAsync(
        ZipArchive archive,
        string directory,
        TerrainAssetType assetType,
        List<TerrainAssetEntry> assets)
    {
        var entries = archive.Entries
            .Where(e => e.FullName.StartsWith(directory, StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.IndexOf('/', directory.Length) == -1)
            .ToList();

        foreach (var entry in entries)
        {
            var fileName = Path.GetFileNameWithoutExtension(entry.Name);
            var parsed = ParseAssetFileName(fileName);
            if (parsed == null) continue;

            var imageBytes = await ReadEntryBytesAsync(entry);
            assets.Add(new TerrainAssetEntry(assetType, parsed.AssetName, parsed.Variant, imageBytes));
        }
    }

    private async Task ExtractEdgeImagesAsync(ZipArchive archive, List<TerrainAssetEntry> assets)
    {
        const string edgesDirectory = "edges/";
        var edgeEntries = archive.Entries
            .Where(e => e.FullName.StartsWith(edgesDirectory, StringComparison.OrdinalIgnoreCase) &&
                        e.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var entry in edgeEntries)
        {
            var fileName = Path.GetFileNameWithoutExtension(entry.Name);
            var parsed = ParseEdgeFileName(fileName);
            if (parsed == null) continue;

            var assetType = parsed.EdgeType == "top" ? TerrainAssetType.EdgeTop : TerrainAssetType.EdgeBottom;
            var imageBytes = await ReadEntryBytesAsync(entry);
            assets.Add(new TerrainAssetEntry(assetType, parsed.Direction, parsed.Variant, imageBytes));
        }
    }

    private static async Task<byte[]> ReadEntryBytesAsync(ZipArchiveEntry entry)
    {
        await using var stream = await entry.OpenAsync();
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private record AssetInfo(string AssetName, int Variant);
    private record EdgeInfo(string EdgeType, string Direction, int Variant);

    /// <summary>
    /// Parses an asset file name into asset name and zero-based variant number.
    /// Returns null when the file has an invalid (non-integer) variant suffix so the asset is skipped.
    /// Examples: "base" -> ("base", 0), "base-1" -> ("base", 1), "base-abc" -> null
    /// </summary>
    private static AssetInfo? ParseAssetFileName(string fileName)
    {
        var normalizedFileName = fileName.ToLowerInvariant();
        var lastDashIndex = fileName.LastIndexOf('-');
        if (lastDashIndex < 0)
            return new AssetInfo(normalizedFileName, 0);

        var namePart = fileName[..lastDashIndex];
        var variantPart = fileName[(lastDashIndex + 1)..];

        return TryParseVariantSuffix(variantPart, out var variant)
            ? new AssetInfo(namePart.ToLowerInvariant(), variant)
            : null;
    }

    /// <summary>
    /// Parses an edge file name into edge type, direction, and zero-based variant number.
    /// Returns null when the file name is invalid or has an invalid variant suffix so the asset is skipped.
    /// Examples: "top-0" -> ("top", "0", 0), "top-0-1" -> ("top", "0", 1), "top-0-abc" -> null
    /// </summary>
    private static EdgeInfo? ParseEdgeFileName(string fileName)
    {
        var parts = fileName.Split('-');

        if (parts.Length is < 2 or > 3)
            return null;

        var edgeType = parts[0].ToLowerInvariant();
        if (edgeType is not ("top" or "bottom"))
            return null;

        var direction = parts[1];
        if (!int.TryParse(direction, out _))
            return null;

        if (parts.Length == 2)
            return new EdgeInfo(edgeType, direction, 0);

        return TryParseVariantSuffix(parts[2], out var variant)
            ? new EdgeInfo(edgeType, direction, variant)
            : null;
    }

    private static bool TryParseVariantSuffix(string variantPart, out int variant)
    {
        variant = 0;
        if (!int.TryParse(variantPart, out var variantNum) || variantNum <= 0)
            return false;

        variant = variantNum;
        return true;
    }
}