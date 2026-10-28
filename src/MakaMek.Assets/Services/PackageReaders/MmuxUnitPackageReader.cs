using System.IO.Compression;
using System.Text.Json;
using Sanet.MakaMek.Assets.Models.Units;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Assets.Services.PackageReaders;

/// <summary>
/// Parsed contents of an MMUX unit package: the unit data, its static image and, for v2 packages,
/// the optional sprite atlas with its metadata.
/// </summary>
/// <param name="Data">Unit data from <c>unit.json</c>.</param>
/// <param name="Image">Static <c>unit.png</c>, always present and used as the reduced-motion/idle fallback.</param>
/// <param name="Animation">Optional v2 sprite atlas and metadata; <c>null</c> for v1 or broken sprite entries.</param>
/// <param name="SpriteError">Why an optional v2 sprite payload was skipped, so the caller can log it.</param>
public sealed record UnitPackage(
    UnitData Data,
    byte[] Image,
    UnitAnimationData? Animation = null,
    string? SpriteError = null);

/// <summary>
/// Format-specific reader for MMUX unit packages (<c>unit.json</c> + <c>unit.png</c>, plus the optional
/// <c>unit-sprites.png</c> / <c>unit-sprites.json</c> pair in manifest version 2).
/// </summary>
/// <remarks>
/// v2 is additive: the static entries stay authoritative and older readers can ignore the manifest and
/// keep loading the unit. A missing manifest or a <c>1.x</c> version is read exactly as before.
/// </remarks>
public class MmuxUnitPackageReader
{
    private const string ManifestEntryName = "manifest.json";
    private const string UnitJsonEntryName = "unit.json";
    private const string UnitImageEntryName = "unit.png";
    private const string SpriteSheetImageEntryName = "unit-sprites.png";
    private const string SpriteSheetMetadataEntryName = "unit-sprites.json";

    /// <summary>Highest manifest major version this reader understands.</summary>
    private const int SupportedMajorVersion = 2;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new EnumConverter<MakaMekComponent>(),
            new EnumConverter<PartLocation>(),
            new EnumConverter<MovementType>(),
            new EnumConverter<UnitStatus>(),
            new EnumConverter<WeightClass>()
        }
    };

    /// <summary>
    /// Reads an MMUX unit package stream.
    /// </summary>
    /// <param name="mmuxStream">Stream containing the MMUX package data.</param>
    /// <param name="cancellationToken">Optional cancellation token propagated to all async I/O.</param>
    /// <returns>The parsed unit package.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the package is missing required entries, its manifest major version is unsupported, or the
    /// unit data is invalid.
    /// </exception>
    public async Task<UnitPackage> Read(Stream mmuxStream, CancellationToken cancellationToken = default)
    {
        await using var archive = new ZipArchive(mmuxStream, ZipArchiveMode.Read);

        var majorVersion = await ReadManifestMajorVersion(archive, cancellationToken);

        var unitJsonEntry = archive.GetEntry(UnitJsonEntryName)
                            ?? throw new InvalidOperationException("MMUX package missing unit.json");

        UnitData unitData;
        await using (var unitJsonStream = await unitJsonEntry.OpenAsync(cancellationToken))
        using (var reader = new StreamReader(unitJsonStream))
        {
            var jsonContent = await reader.ReadToEndAsync(cancellationToken);
            unitData = JsonSerializer.Deserialize<UnitData>(jsonContent, _jsonOptions);
            if (string.IsNullOrEmpty(unitData.Model))
            {
                throw new InvalidOperationException("Failed to deserialize unit.json");
            }
        }

        var unitImageEntry = archive.GetEntry(UnitImageEntryName)
                             ?? throw new InvalidOperationException("MMUX package missing unit.png");
        var imageBytes = await ReadEntryBytes(unitImageEntry, cancellationToken);

        if (majorVersion < 2)
        {
            return new UnitPackage(unitData, imageBytes);
        }

        var (animation, spriteError) = await ReadSprites(archive, cancellationToken);
        return new UnitPackage(unitData, imageBytes, animation, spriteError);
    }

    /// <summary>
    /// Reads the manifest and returns the major version it declares. A missing or unparsable manifest is
    /// treated as v1 so packages written before the manifest existed keep loading.
    /// </summary>
    private static async Task<int> ReadManifestMajorVersion(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(ManifestEntryName);
        if (entry is null) return 1;

        string json;
        await using (var stream = await entry.OpenAsync(cancellationToken))
        using (var reader = new StreamReader(stream))
        {
            json = await reader.ReadToEndAsync(cancellationToken);
        }

        string? version;
        try
        {
            using var document = JsonDocument.Parse(json);
            version = document.RootElement.TryGetProperty("version", out var versionElement)
                      && versionElement.ValueKind == JsonValueKind.String
                ? versionElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            // A corrupt manifest must not make the unit unloadable; the v1 entries are self-sufficient.
            return 1;
        }

        if (string.IsNullOrWhiteSpace(version)) return 1;

        var majorText = version.Split('.', 2)[0];
        if (!int.TryParse(majorText, out var major)) return 1;

        if (major < 1)
        {
            throw new InvalidOperationException($"Invalid MMUX manifest version '{version}'.");
        }

        if (major > SupportedMajorVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported MMUX manifest version '{version}'. " +
                $"This reader supports versions 1.x and 2.x.");
        }

        return major;
    }

    /// <summary>
    /// Reads the optional v2 sprite atlas. A broken or incomplete payload never fails the unit: it is
    /// reported through <see cref="UnitPackage.SpriteError"/> and the static image is used instead.
    /// </summary>
    private async Task<(UnitAnimationData? Animation, string? Error)> ReadSprites(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var imageEntry = archive.GetEntry(SpriteSheetImageEntryName);
        var metadataEntry = archive.GetEntry(SpriteSheetMetadataEntryName);

        if (imageEntry is null && metadataEntry is null)
        {
            return (null, null);
        }

        if (imageEntry is null || metadataEntry is null)
        {
            var missing = imageEntry is null ? SpriteSheetImageEntryName : SpriteSheetMetadataEntryName;
            return (null, $"MMUX v2 package is missing {missing}; using the static unit image.");
        }

        try
        {
            var atlas = await ReadEntryBytes(imageEntry, cancellationToken);

            UnitSpriteSheet? sheet;
            await using (var metadataStream = await metadataEntry.OpenAsync(cancellationToken))
            {
                sheet = await JsonSerializer.DeserializeAsync<UnitSpriteSheet>(metadataStream, _jsonOptions,
                    cancellationToken);
            }

            if (sheet is null || !sheet.IsValid)
            {
                return (null, "MMUX v2 package has invalid unit-sprites.json; using the static unit image.");
            }

            return (new UnitAnimationData(atlas, sheet), null);
        }
        catch (JsonException ex)
        {
            return (null, $"MMUX v2 package has malformed unit-sprites.json ({ex.Message}); using the static unit image.");
        }
        catch (InvalidOperationException ex)
        {
            return (null, $"MMUX v2 package sprite atlas is unreadable ({ex.Message}); using the static unit image.");
        }
    }

    private static async Task<byte[]> ReadEntryBytes(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var stream = await entry.OpenAsync(cancellationToken);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }
}
