using System.IO.Compression;
using System.Text.Json;
using Sanet.MakaMek.Assets.Services.PackageReaders;
using Sanet.MakaMek.Core.Data.Serialization.Converters;
using Sanet.MakaMek.Core.Data.Units;
using Sanet.MakaMek.Core.Data.Units.Components;
using Sanet.MakaMek.Core.Models.Units;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Assets.Tests.Services.PackageReaders;

public class MmuxUnitPackageReaderTests
{
    private readonly MmuxUnitPackageReader _sut = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters =
        {
            new EnumConverter<MakaMekComponent>(),
            new EnumConverter<PartLocation>(),
            new EnumConverter<MovementType>(),
            new EnumConverter<UnitStatus>(),
            new EnumConverter<WeightClass>()
        }
    };

    private static readonly byte[] DefaultImage = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] DefaultAtlas = [0x89, 0x50, 0x4E, 0x47, 0x0D];

    private const string ValidSpriteMetadataJson = """
        {
          "cellWidth": 16,
          "cellHeight": 18,
          "padding": 2,
          "pivot": [0.5, 0.5],
          "distanceUnits": "spritePixels",
          "animations": {
            "Walk": { "row": 0, "frames": 2, "fps": 12, "loop": true, "distancePerCycle": 21.5 },
            "Run": { "row": 1, "frames": 2, "fps": 16, "loop": true, "distancePerCycle": 43.0 }
          }
        }
        """;

    private static string UnitJson(string model = "LCT-1V", string chassis = "Locust") =>
        JsonSerializer.Serialize(new UnitData
        {
            Model = model,
            Chassis = chassis,
            Mass = 20,
            EngineRating = 160,
            EngineType = "Standard",
            ArmorValues = new Dictionary<PartLocation, ArmorLocation>(),
            Equipment = new List<ComponentData>(),
            AdditionalAttributes = new Dictionary<string, string>(),
            Quirks = new Dictionary<string, string>()
        }, JsonOptions);

    private static MemoryStream CreatePackage(
        string? manifestJson = null,
        bool includeUnitJson = true,
        string? unitJson = null,
        bool includeImage = true,
        bool includeSpriteImage = false,
        bool includeSpriteMetadata = false,
        string? spriteMetadataJson = null)
    {
        var memoryStream = new MemoryStream();

        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            if (manifestJson is not null)
            {
                AddTextEntry(archive, "manifest.json", manifestJson);
            }

            if (includeUnitJson)
            {
                AddTextEntry(archive, "unit.json", unitJson ?? UnitJson());
            }

            if (includeImage)
            {
                AddBinaryEntry(archive, "unit.png", DefaultImage);
            }

            if (includeSpriteImage)
            {
                AddBinaryEntry(archive, "unit-sprites.png", DefaultAtlas);
            }

            if (includeSpriteMetadata)
            {
                AddTextEntry(archive, "unit-sprites.json", spriteMetadataJson ?? ValidSpriteMetadataJson);
            }
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    private static void AddTextEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    private static void AddBinaryEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(content, 0, content.Length);
    }

    private static string Manifest(string version) => $$"""{"version":"{{version}}","unitId":"LCT-1V"}""";

    [Fact]
    public async Task Read_WithoutManifest_ReturnsV1PackageWithoutAnimation()
    {
        await using var package = CreatePackage();

        var result = await _sut.Read(package);

        result.Data.Model.ShouldBe("LCT-1V");
        result.Data.Chassis.ShouldBe("Locust");
        result.Image.ShouldBe(DefaultImage);
        result.Animation.ShouldBeNull();
        result.SpriteError.ShouldBeNull();
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.5")]
    public async Task Read_WithV1Manifest_IgnoresSpriteEntries(string version)
    {
        // v1 packages may still carry sprite entries; only the manifest major version decides.
        await using var package = CreatePackage(
            Manifest(version), includeSpriteImage: true, includeSpriteMetadata: true);

        var result = await _sut.Read(package);

        result.Animation.ShouldBeNull();
        result.SpriteError.ShouldBeNull();
    }

    [Fact]
    public async Task Read_WithV2ManifestAndSprites_ReturnsAnimation()
    {
        await using var package = CreatePackage(
            Manifest("2.0"), includeSpriteImage: true, includeSpriteMetadata: true);

        var result = await _sut.Read(package);

        result.Animation.ShouldNotBeNull();
        result.Animation.Atlas.ShouldBe(DefaultAtlas);
        result.Animation.Sheet.CellWidth.ShouldBe(16);
        result.Animation.Sheet.CellHeight.ShouldBe(18);
        result.Animation.Sheet.Padding.ShouldBe(2);
        result.Animation.Sheet.DistanceUnits.ShouldBe("spritePixels");
        result.Animation.Sheet.Animations["Walk"].DistancePerCycle.ShouldBe(21.5);
        result.Animation.Sheet.Animations["Run"].Row.ShouldBe(1);
        result.SpriteError.ShouldBeNull();
    }

    [Fact]
    public async Task Read_WithV2ManifestAndNoSpriteEntries_ReturnsNoAnimation()
    {
        await using var package = CreatePackage(Manifest("2.0"));

        var result = await _sut.Read(package);

        result.Animation.ShouldBeNull();
        result.SpriteError.ShouldBeNull();
    }

    [Fact]
    public async Task Read_WithV2ManifestAndOnlySpriteImage_ReportsMissingMetadata()
    {
        await using var package = CreatePackage(Manifest("2.0"), includeSpriteImage: true);

        var result = await _sut.Read(package);

        result.Animation.ShouldBeNull();
        result.SpriteError.ShouldNotBeNull();
        result.SpriteError.ShouldContain("unit-sprites.json");
        result.Data.Model.ShouldBe("LCT-1V");
    }

    [Fact]
    public async Task Read_WithV2ManifestAndOnlySpriteMetadata_ReportsMissingImage()
    {
        await using var package = CreatePackage(Manifest("2.0"), includeSpriteMetadata: true);

        var result = await _sut.Read(package);

        result.Animation.ShouldBeNull();
        result.SpriteError.ShouldNotBeNull();
        result.SpriteError.ShouldContain("unit-sprites.png");
        result.Data.Model.ShouldBe("LCT-1V");
    }

    [Fact]
    public async Task Read_WithV2ManifestAndMalformedSpriteMetadata_ReportsError()
    {
        await using var package = CreatePackage(
            Manifest("2.0"),
            includeSpriteImage: true,
            includeSpriteMetadata: true,
            spriteMetadataJson: "{ \"cellWidth\": 16, oops }");

        var result = await _sut.Read(package);

        result.Animation.ShouldBeNull();
        result.SpriteError.ShouldNotBeNull();
        result.SpriteError.ShouldContain("malformed unit-sprites.json");
        result.Data.Model.ShouldBe("LCT-1V");
    }

    [Theory]
    [InlineData("""{ "cellWidth": 0, "cellHeight": 18, "animations": { "Walk": { "frames": 2 } } }""")]
    [InlineData("""{ "cellWidth": 16, "cellHeight": 18, "padding": -1, "animations": { "Walk": { "frames": 2 } } }""")]
    [InlineData("""{ "cellWidth": 16, "cellHeight": 18, "animations": {} }""")]
    [InlineData("""{ "cellWidth": 16, "cellHeight": 18, "animations": { "Walk": { "frames": 0 } } }""")]
    [InlineData("null")]
    public async Task Read_WithV2ManifestAndInvalidSpriteSheet_ReportsError(string spriteMetadataJson)
    {
        await using var package = CreatePackage(
            Manifest("2.0"),
            includeSpriteImage: true,
            includeSpriteMetadata: true,
            spriteMetadataJson: spriteMetadataJson);

        var result = await _sut.Read(package);

        result.Animation.ShouldBeNull();
        result.SpriteError.ShouldNotBeNull();
        result.SpriteError.ShouldContain("invalid unit-sprites.json");
        result.Data.Model.ShouldBe("LCT-1V");
    }

    [Fact]
    public async Task Read_WithCorruptManifest_TreatsPackageAsV1()
    {
        await using var package = CreatePackage("{ this is not json", includeSpriteImage: true,
            includeSpriteMetadata: true);

        var result = await _sut.Read(package);

        result.Animation.ShouldBeNull();
        result.SpriteError.ShouldBeNull();
        result.Data.Model.ShouldBe("LCT-1V");
    }

    [Theory]
    [InlineData("""{"unitId":"LCT-1V"}""")]
    [InlineData("""{"version":"","unitId":"LCT-1V"}""")]
    [InlineData("""{"version":"   ","unitId":"LCT-1V"}""")]
    [InlineData("""{"version":"vNext","unitId":"LCT-1V"}""")]
    [InlineData("""{"version":2,"unitId":"LCT-1V"}""")]
    [InlineData("""[1,2,3]""")]
    public async Task Read_WithUnusableManifestVersion_TreatsPackageAsV1(string manifestJson)
    {
        await using var package = CreatePackage(manifestJson, includeSpriteImage: true,
            includeSpriteMetadata: true);

        var result = await _sut.Read(package);

        result.Animation.ShouldBeNull();
        result.SpriteError.ShouldBeNull();
        result.Data.Model.ShouldBe("LCT-1V");
    }

    [Theory]
    [InlineData("0.9")]
    [InlineData("0.0")]
    public async Task Read_WithMajorVersionBelowOne_Throws(string version)
    {
        await using var package = CreatePackage(Manifest(version));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Read(package));

        exception.Message.ShouldContain("Invalid MMUX manifest version");
        exception.Message.ShouldContain(version);
    }

    [Fact]
    public async Task Read_WithUnsupportedMajorVersion_Throws()
    {
        await using var package = CreatePackage(Manifest("3.0"));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Read(package));

        exception.Message.ShouldContain("Unsupported MMUX manifest version");
        exception.Message.ShouldContain("1.x and 2.x");
    }

    [Fact]
    public async Task Read_WithoutUnitJson_Throws()
    {
        await using var package = CreatePackage(Manifest("2.0"), includeUnitJson: false);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Read(package));

        exception.Message.ShouldBe("MMUX package missing unit.json");
    }

    [Fact]
    public async Task Read_WithoutUnitImage_Throws()
    {
        await using var package = CreatePackage(Manifest("2.0"), includeImage: false);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Read(package));

        exception.Message.ShouldBe("MMUX package missing unit.png");
    }

    [Fact]
    public async Task Read_WithUnitJsonWithoutModel_Throws()
    {
        await using var package = CreatePackage(unitJson: UnitJson(model: string.Empty));

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _sut.Read(package));

        exception.Message.ShouldBe("Failed to deserialize unit.json");
    }

    [Fact]
    public async Task Read_WhenCancelled_Throws()
    {
        await using var package = CreatePackage();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => _sut.Read(package, cancellation.Token));
    }
}
