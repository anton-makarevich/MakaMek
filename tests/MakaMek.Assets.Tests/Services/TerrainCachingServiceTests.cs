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
        using var mmtxStream = CreateMmtxPackage("test-biome", "Test Biome", "1.0.0");

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
        using var mmtxStream = CreateMmtxPackage("", "Test Biome", "1.0.0");

        // Act
        var manifest = await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Assert
        manifest.ShouldBeNull();
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldLoadBaseTerrain()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithBaseTerrain("test-biome");

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
        using var mmtxStream = CreateMmtxPackageWithOverlays("test-biome");

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
        using var mmtxStream = CreateMmtxPackageWithEdges("test-biome");

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
        using var mmtxStream = CreateMmtxPackageWithOptionalAssetVariants("test-biome");

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
        using var mmtxStream = CreateMmtxPackageWithOptionalEdgeVariants("test-biome");

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
        using var mmtxStream = CreateMmtxPackageWithMultipleVariants("test-biome");

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
        using var mmtxStream = CreateMmtxPackageWithBaseTerrain("test-biome");
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
        using var mmtxStream = CreateMmtxPackageWithOverlays("test-biome");
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
        using var mmtxStream = CreateMmtxPackageWithOverlays("test-biome");
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
        using var mmtxStream = CreateMmtxPackageWithOverlays("test-biome");
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
        using var mmtxStream = CreateMmtxPackageWithEdges("test-biome");
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
        using var mmtxStream = CreateMmtxPackageWithEdges("test-biome");
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
        using var mmtxStream = CreateMmtxPackageWithMultipleEdgeVariants("test-biome");
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
        using var mmtxStream = CreateMmtxPackageWithMultipleEdgeVariants("test-biome");
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
        using var mmtxStream1 = CreateMmtxPackage("biome1", "Biome 1", "1.0.0");
        using var mmtxStream2 = CreateMmtxPackage("biome2", "Biome 2", "1.0.0");
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
        using var mmtxStream = CreateMmtxPackageWithBaseTerrain("test-biome");
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
        using var mmtxStream = CreateMmtxPackageWithNonIntegerVariant("test-biome");

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
        using var firstStream = CreateMmtxPackageWithBaseTerrain("duplicate-biome");
        using var secondStream = CreateMmtxPackageWithBaseTerrain("duplicate-biome");

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
        loadedBiomes.ShouldContain("duplicate-biome");
        
        // Verify variants are only from the first load
        var variants = await _sut.GetAvailableVariants("duplicate-biome", TerrainAssetType.Base, "base");
        variants.Count.ShouldBe(1);
        variants.ShouldContain(1);
    }
     
    private static MemoryStream CreateMmtxPackage(string biomeId, string name, string version)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new
                {
                    id = biomeId,
                    name,
                    version
                };
                writer.Write(JsonSerializer.Serialize(manifest));
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithoutManifest()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add a dummy file instead of a manifest
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

    private static MemoryStream CreateMmtxPackageWithBaseTerrain(string biomeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name = "Test Biome", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            // Add base terrain image
            var baseEntry = archive.CreateEntry("base-1.png");
            using (var baseStream = baseEntry.Open())
            {
                baseStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4); // PNG header
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithOverlays(string biomeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name = "Test Biome", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            // Add terrain overlay images
            var lightWoodsEntry = archive.CreateEntry("terrains/lightwoods-1.png");
            using (var lightWoodsStream = lightWoodsEntry.Open())
            {
                lightWoodsStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }

            var heavyWoodsEntry = archive.CreateEntry("terrains/heavywoods-1.png");
            using (var heavyWoodsStream = heavyWoodsEntry.Open())
            {
                heavyWoodsStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }

            // Add rough terrain overlay (variant 0 — no suffix)
            var roughEntry = archive.CreateEntry("terrains/rough.png");
            using (var roughStream = roughEntry.Open())
            {
                roughStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithEdges(string biomeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name = "Test Biome", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            // Add edge images
            var topEdgeEntry = archive.CreateEntry("edges/top-0-1.png");
            using (var topEdgeStream = topEdgeEntry.Open())
            {
                topEdgeStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }

            var bottomEdgeEntry = archive.CreateEntry("edges/bottom-3-1.png");
            using (var bottomEdgeStream = bottomEdgeEntry.Open())
            {
                bottomEdgeStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithMultipleVariants(string biomeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name = "Test Biome", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            // Add multiple base terrain variants
            for (var i = 1; i <= 3; i++)
            {
                var baseEntry = archive.CreateEntry($"base-{i}.png");
                using var baseStream = baseEntry.Open();
                baseStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithOptionalAssetVariants(string biomeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name = "Test Biome", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            foreach (var path in new[] { "base.png", "base-1.png", "terrains/lightwoods.png", "terrains/lightwoods-2.png" })
            {
                var entry = archive.CreateEntry(path);
                using var entryStream = entry.Open();
                entryStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithOptionalEdgeVariants(string biomeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name = "Test Biome", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            foreach (var path in new[] { "edges/top-0.png", "edges/top-0-1.png", "edges/bottom-3-2.png" })
            {
                var entry = archive.CreateEntry(path);
                using var entryStream = entry.Open();
                entryStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithMultipleEdgeVariants(string biomeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name = "Test Biome", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            // Add multiple edge variants for direction 0 with unique content per variant
            for (int i = 1; i <= 3; i++)
            {
                var edgeEntry = archive.CreateEntry($"edges/top-0-{i}.png");
                using var edgeStream = edgeEntry.Open();
                edgeStream.Write([0x89, 0x50, 0x4E, 0x47, (byte)i], 0, 5); // unique trailing byte per variant
            }
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task GetWaterTextureImage_ShouldReturnImage_WhenExists()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithWaterTextures("test-biome");
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
        using var mmtxStream = CreateMmtxPackageWithWaterTextures("test-biome");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        // Bitmask 0b111111 (63) doesn't exist in the package
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
        using var mmtxStream = CreateMmtxPackageWithMultipleWaterTextures("test-biome");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var variantsForMask1 = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Water, "000001");
        var variantsForMask3 = await _sut.GetAvailableVariants("test-biome", TerrainAssetType.Water, "000011");

        // Assert
        variantsForMask1.Count.ShouldBe(1);
        variantsForMask1.ShouldContain(1);
        variantsForMask3.Count.ShouldBe(1);
        variantsForMask3.ShouldContain(1);
    }

    private static MemoryStream CreateMmtxPackageWithWaterTextures(string biomeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name = "Test Biome", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            // Add water texture for bitmask 000001 (mask value 1)
            var waterEntry = archive.CreateEntry("terrains/water/000001-1.png");
            using (var waterStream = waterEntry.Open())
            {
                waterStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithMultipleWaterTextures(string biomeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name = "Test Biome", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            // Add water textures for multiple bitmasks
            var waterEntry1 = archive.CreateEntry("terrains/water/000001-1.png");
            using (var waterStream1 = waterEntry1.Open())
            {
                waterStream1.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }

            var waterEntry3 = archive.CreateEntry("terrains/water/000011-1.png");
            using (var waterStream3 = waterEntry3.Open())
            {
                waterStream3.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithNonIntegerVariant(string biomeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { id = biomeId, name = "Test Biome", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            // Add base terrain with noninteger variant (should be ignored)
            var baseEntry = archive.CreateEntry("base-abc.png");
            using (var baseStream = baseEntry.Open())
            {
                baseStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }

            // Add valid base terrain to have something to test against
            var validBaseEntry = archive.CreateEntry("base.png");
            using (var validBaseStream = validBaseEntry.Open())
            {
                validBaseStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }
        }
        stream.Position = 0;
        return stream;
    }
}
