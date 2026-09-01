using System.IO.Compression;
using System.Text.Json;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;

namespace Sanet.MakaMek.Assets.Services.PackageReaders;

/// <summary>
/// Parsed contents of an MMUX unit package: the unit data and its image bytes
/// </summary>
public sealed record UnitPackage(UnitData Data, byte[] Image);

/// <summary>
/// Format-specific reader for MMUX unit packages (<c>unit.json</c> + <c>unit.png</c>)
/// </summary>
public class MmuxUnitPackageReader
{
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
    /// Reads an MMUX unit package stream
    /// </summary>
    /// <param name="mmuxStream">Stream containing the MMUX package data</param>
    /// <returns>The parsed unit package</returns>
    /// <exception cref="InvalidOperationException">Thrown when the package is missing required entries or data is invalid</exception>
    public async Task<UnitPackage> ReadAsync(Stream mmuxStream)
    {
        await using var archive = new ZipArchive(mmuxStream, ZipArchiveMode.Read);

        // Find and load unit.json
        var unitJsonEntry = archive.GetEntry("unit.json");
        if (unitJsonEntry == null)
        {
            throw new InvalidOperationException("MMUX package missing unit.json");
        }

        UnitData unitData;
        await using (var unitJsonStream = await unitJsonEntry.OpenAsync())
        using (var reader = new StreamReader(unitJsonStream))
        {
            var jsonContent = await reader.ReadToEndAsync();
            unitData = JsonSerializer.Deserialize<UnitData>(jsonContent, _jsonOptions);
            if (string.IsNullOrEmpty(unitData.Model))
            {
                throw new InvalidOperationException("Failed to deserialize unit.json");
            }
        }

        // Find and load unit.png
        var unitImageEntry = archive.GetEntry("unit.png");
        if (unitImageEntry == null)
        {
            throw new InvalidOperationException("MMUX package missing unit.png");
        }

        byte[] imageBytes;
        await using (var imageStream = await unitImageEntry.OpenAsync())
        using (var memoryStream = new MemoryStream())
        {
            await imageStream.CopyToAsync(memoryStream);
            imageBytes = memoryStream.ToArray();
        }

        return new UnitPackage(unitData, imageBytes);
    }
}