using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Sanet.MakaMek.Assets.Models.Terrains;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Assets.Tests.Services;

public class TerrainCachingServiceTests
{
    private readonly ILoggerFactory _loggerFactory= Substitute.For<ILoggerFactory>();
    private readonly TerrainCachingService _sut;

    public TerrainCachingServiceTests()
    {
        _loggerFactory.CreateLogger(Arg.Any<string>())
            .Returns(Substitute.For<ILogger>());
        _sut = new TerrainCachingService([], _loggerFactory);
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldLoadManifest()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage();

        // Act
        var manifest = await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Assert
        manifest.ShouldNotBeNull();
        manifest.Id.ShouldBe("test-biome");
        manifest.Name.ShouldBe("Test Biome");
        manifest.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldReturnNull_WhenMissingManifest()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithoutManifest();

        // Act
        var manifest = await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Assert
        manifest.ShouldBeNull();
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldReturnNull_WhenMissingBiomeId()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage("");

        // Act
        var manifest = await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Assert
        manifest.ShouldBeNull();
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldLoadBaseTerrain()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create().WithBaseTerrain(1));

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var variants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Base, "base");

        // Assert
        variants.Count.ShouldBe(1);
        variants.ShouldContain(1);
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldLoadTerrainOverlays()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithOverlay("lightwoods", 1)
            .WithOverlay("heavywoods", 1)
            .WithOverlay("rough", 0));

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var lightwoodsVariants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Overlay, "lightwoods");
        var heavyWoodsVariants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Overlay, "heavywoods");
        var roughVariants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Overlay, "rough");

        // Assert
        lightwoodsVariants.Count.ShouldBe(1);
        heavyWoodsVariants.Count.ShouldBe(1);
        roughVariants.Count.ShouldBe(1);
        roughVariants.ShouldContain(0); // rough.png has no suffix → variant 0
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldLoadEdgeImages()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithEdge("top", "0", 1)
            .WithEdge("bottom", "3", 1));

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var topEdgeVariants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.EdgeTop, "0");
        var bottomEdgeVariants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.EdgeBottom, "3");

        // Assert
        topEdgeVariants.Count.ShouldBe(1);
        bottomEdgeVariants.Count.ShouldBe(1);
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldTreatOptionalAssetVariantsAsOneBased()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithBaseTerrain(0, 1)
            .WithOverlay("lightwoods", 0, 2));

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var baseVariants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Base, "base");
        var lightwoodsVariants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Overlay, "lightwoods");

        // Assert
        baseVariants.Count.ShouldBe(2);
        baseVariants.ShouldContain(0);
        baseVariants.ShouldContain(1);

        lightwoodsVariants.Count.ShouldBe(2);
        lightwoodsVariants.ShouldContain(0);
        lightwoodsVariants.ShouldContain(2);
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldTreatOptionalEdgeVariantsAsOneBased()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithEdge("top", "0", 0, 1)
            .WithEdge("bottom", "3", 2));

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var topEdgeVariants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.EdgeTop, "0");
        var bottomEdgeVariants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.EdgeBottom, "3");

        // Assert
        topEdgeVariants.Count.ShouldBe(2);
        topEdgeVariants.ShouldContain(0);
        topEdgeVariants.ShouldContain(1);

        bottomEdgeVariants.Count.ShouldBe(1);
        bottomEdgeVariants.ShouldContain(2);
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldLoadMultipleVariants()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create().WithBaseTerrain(1, 2, 3));

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var variants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Base, "base");

        // Assert
        variants.Count.ShouldBe(3);
        variants.ShouldContain(1);
        variants.ShouldContain(2);
        variants.ShouldContain(3);
    }

    [Fact]
    public async Task GetBaseTerrainImage_ShouldReturnImage_WhenLoaded()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create().WithBaseTerrain(1));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var image = await _sut.GetBaseBiomeImage("test-biome");

        // Assert
        image.ShouldNotBeNull();
        image.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetBaseTerrainImage_ShouldReturnNull_WhenBiomeNotLoaded()
    {
        // Act
        var image = await _sut.GetBaseBiomeImage("nonexistent-biome");

        // Assert
        image.ShouldBeNull();
    }

    [Fact]
    public async Task GetTerrainOverlayImage_ShouldReturnImage_WhenExists()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create().WithOverlay("lightwoods", 1));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var image = await _sut.GetTerrainOverlayImage("test-biome", "lightwoods");

        // Assert
        image.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetTerrainOverlayImage_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create().WithOverlay("lightwoods", 1));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var image = await _sut.GetTerrainOverlayImage("test-biome", "nonexistent");

        // Assert
        image.ShouldBeNull();
    }

    [Fact]
    public async Task GetTerrainOverlayImage_ShouldReturnImage_ForRoughTerrain()
    {
        // Arrange — rough.png is a variant-0 asset (no suffix)
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create().WithOverlay("rough", 0));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var image = await _sut.GetTerrainOverlayImage("test-biome", "rough");

        // Assert
        image.ShouldNotBeNull();
        image.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetEdgeImage_ShouldReturnImage_WhenExists()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithEdge("top", "0", 1)
            .WithEdge("bottom", "3", 1));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var coordinates = new HexCoordinates(0, 0);

        // Act
        var image = await _sut.GetEdgeImage("test-biome", HexDirection.Top, TerrainAssetType.EdgeTop, coordinates);

        // Assert
        image.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetEdgeImage_ShouldReturnNull_WhenInvalidEdgeType()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithEdge("top", "0", 1)
            .WithEdge("bottom", "3", 1));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var coordinates = new HexCoordinates(0, 0);

        // Act
        var image = await _sut.GetEdgeImage("test-biome", HexDirection.Top, TerrainAssetType.Base, coordinates);

        // Assert
        image.ShouldBeNull();
    }

    [Fact]
    public async Task GetEdgeImage_ShouldReturnConsistentVariant_ForSameCoordinates()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithEdgeUniqueContent("top", "0", 1, 2, 3));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var coordinates = new HexCoordinates(5, 10);

        // Act - Get the same edge twice
        var image1 = await _sut.GetEdgeImage("test-biome", HexDirection.Top, TerrainAssetType.EdgeTop, coordinates);
        var image2 = await _sut.GetEdgeImage("test-biome", HexDirection.Top, TerrainAssetType.EdgeTop, coordinates);

        // Assert - Should return the same variant (deterministic)
        image1.ShouldBe(image2);
    }

    [Fact]
    public async Task GetEdgeImage_ShouldUseMultipleVariants_AcrossDifferentCoordinates()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithEdgeUniqueContent("top", "0", 1, 2, 3));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var distinctImages = new HashSet<string>();
        for (var i = 0; i < 32; i++)
        {
            var image = await _sut.GetEdgeImage(
                "test-biome",
                HexDirection.Top,
                TerrainAssetType.EdgeTop,
                new HexCoordinates(i, i * 7));
            image.ShouldNotBeNull();
            distinctImages.Add(Convert.ToHexString(image));
        }

        // Assert
        distinctImages.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task GetLoadedBiomes_ShouldReturnEmpty_WhenNothingLoaded()
    {
        // Act
        var biomes = await _sut.GetLoadedBiomes();

        // Assert
        biomes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLoadedBiomes_ShouldReturnLoadedBiomes()
    {
        // Arrange
        using var mmtxStream1 = CreateMmtxPackage("biome1", "Biome 1");
        using var mmtxStream2 = CreateMmtxPackage("biome2", "Biome 2");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream1);
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream2);

        // Act
        var biomes = (await _sut.GetLoadedBiomes()).ToList();

        // Assert
        biomes.Count.ShouldBe(2);
        biomes.ShouldContain("biome1");
        biomes.ShouldContain("biome2");
    }

    [Fact]
    public async Task GetBiomeManifest_ShouldReturnManifest_WhenLoaded()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage("test-biome", "Test Biome", "2.0.0");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var manifest = await _sut.GetBiomeManifest("test-biome");

        // Assert
        manifest.ShouldNotBeNull();
        manifest.Version.ShouldBe("2.0.0");
    }

    [Fact]
    public async Task GetBiomeManifest_ShouldReturnNull_WhenNotLoaded()
    {
        // Act
        var manifest = await _sut.GetBiomeManifest("nonexistent");

        // Assert
        manifest.ShouldBeNull();
    }

    [Fact]
    public async Task ClearCache_ShouldClearAllData()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create().WithBaseTerrain(1));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        _sut.ClearCache();
        var biomes = await _sut.GetLoadedBiomes();

        // Assert
        biomes.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldIgnoreAssetWithNonIntegerVariant()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithRawEntry("base-abc.png", PngHeader())
            .WithRawEntry("base.png", PngHeader()));

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var variants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Base, "base");
        var invalidVariants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Base, "base-abc");

        // Assert - base-abc.png is skipped entirely; only base.png (variant 0) is loaded
        variants.Count.ShouldBe(1);
        variants.ShouldContain(0);
        invalidVariants.Count.ShouldBe(0);
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldRejectDuplicateBiomeId()
    {
        // Arrange
        using var firstStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create().WithBaseTerrain(1));
        using var secondStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create().WithBaseTerrain(1));

        // Act - Load the first biome successfully
        var firstManifest = await _sut.LoadTerrainFromMmtxStreamAsync(firstStream);
        firstManifest.ShouldNotBeNull();

        // Attempt to load the duplicate biome
        var secondManifest = await _sut.LoadTerrainFromMmtxStreamAsync(secondStream);

        // Assert
        secondManifest.ShouldBeNull(); // Should return null for duplicate

        // Verify only the first biome is loaded
        var loadedBiomes = (await _sut.GetLoadedBiomes()).ToList();
        loadedBiomes.Count.ShouldBe(1);
        loadedBiomes.ShouldContain("test-biome");

        // Verify variants are only from the first load
        var variants = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Base, "base");
        variants.Count.ShouldBe(1);
        variants.ShouldContain(1);
    }

    [Fact]
    public async Task GetWaterTextureImage_ShouldReturnImage_WhenExists()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithWaterTexture("000001", 1));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var canonicalBitmask = new CanonicalBitmaskResult(0b000001, 0);

        // Act
        var image = await _sut.GetWaterTextureImage("test-biome", canonicalBitmask);

        // Assert
        image.ShouldNotBeNull();
        image.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetWaterTextureImage_ShouldReturnNull_WhenBiomeNotLoaded()
    {
        // Arrange
        var canonicalBitmask = new CanonicalBitmaskResult(0b000001, 0);

        // Act
        var image = await _sut.GetWaterTextureImage("nonexistent-biome", canonicalBitmask);

        // Assert
        image.ShouldBeNull();
    }

    [Fact]
    public async Task GetWaterTextureImage_ShouldReturnNull_WhenNoWaterTextureForBitmask()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithWaterTexture("000001", 1));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var canonicalBitmask = new CanonicalBitmaskResult(0b111111, 0);

        // Act
        var image = await _sut.GetWaterTextureImage("test-biome", canonicalBitmask);

        // Assert
        image.ShouldBeNull();
    }

    [Fact]
    public async Task GetWaterTextureImage_ShouldLoadMultipleBitmasks()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage(builder: MmtxPackageBuilder.Create()
            .WithWaterTexture("000001", 1)
            .WithWaterTexture("000011", 1));
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        // Act
        var imageForMask1 = await _sut.GetWaterTextureImage("test-biome", new CanonicalBitmaskResult(0b000001, 0));
        var imageForMask3 = await _sut.GetWaterTextureImage("test-biome", new CanonicalBitmaskResult(0b000011, 0));
        // Assert
        imageForMask1.ShouldNotBeNull();
        imageForMask3.ShouldNotBeNull();
        imageForMask1.Length.ShouldBeGreaterThan(0);
        imageForMask3.Length.ShouldBeGreaterThan(0);
    }

    private static MemoryStream CreateMmtxPackage(
        string biomeId = "test-biome",
        string name = "Test Biome",
        string version = "1.0.0",
        MmtxPackageBuilder? builder = null)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name, version };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            builder?.AddEntries(archive);
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithoutManifest()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var dummyEntry = archive.CreateEntry("dummy.txt");
            using (var entryStream = dummyEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                writer.Write("dummy content");
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static byte[] PngHeader() => [0x89, 0x50, 0x4E, 0x47];

    private sealed class MmtxPackageBuilder
    {
        private readonly List<(string Path, byte[] Content)> _entries = new();

        public static MmtxPackageBuilder Create() => new();

        public MmtxPackageBuilder WithBaseTerrain(params int[] variants) =>
            AddVariantEntries(variants, v => v == 0 ? "base.png" : $"base-{v}.png");

        public MmtxPackageBuilder WithOverlay(string name, params int[] variants) =>
            AddVariantEntries(variants, v => v == 0 ? $"terrains/{name}.png" : $"terrains/{name}-{v}.png");

        public MmtxPackageBuilder WithEdge(string edgeType, string direction, params int[] variants) =>
            AddVariantEntries(variants, v => v == 0 ? $"edges/{edgeType}-{direction}.png" : $"edges/{edgeType}-{direction}-{v}.png");

        public MmtxPackageBuilder WithEdgeUniqueContent(string edgeType, string direction, params int[] variants) =>
            AddVariantEntries(
                variants,
                v => v == 0 ? $"edges/{edgeType}-{direction}.png" : $"edges/{edgeType}-{direction}-{v}.png",
                v => v == 0 ? PngHeader() : [.. PngHeader(), (byte)v]);

        public MmtxPackageBuilder WithWaterTexture(string bitmask, params int[] variants) =>
            AddVariantEntries(variants, v => v == 0 ? $"terrains/water/{bitmask}.png" : $"terrains/water/{bitmask}-{v}.png");

        private MmtxPackageBuilder AddVariantEntries(
            int[] variants,
            Func<int, string> pathFormatter,
            Func<int, byte[]>? contentFactory = null)
        {
            var content = contentFactory ?? (_ => PngHeader());
            foreach (var v in variants)
            {
                _entries.Add((pathFormatter(v), content(v)));
            }
            return this;
        }

        public MmtxPackageBuilder WithRawEntry(string path, byte[] content)
        {
            _entries.Add((path, content));
            return this;
        }

        public void AddEntries(ZipArchive archive)
        {
            foreach (var (path, content) in _entries)
            {
                var entry = archive.CreateEntry(path);
                using var entryStream = entry.Open();
                entryStream.Write(content, 0, content.Length);
            }
        }
    }
}
