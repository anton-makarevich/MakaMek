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
        using var mmtxStream = CreateMmtxPackage("test-theme", "Test Theme", "1.0.0");

        // Act
        var manifest = await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Assert
        manifest.ShouldNotBeNull();
        manifest.Id.ShouldBe("test-theme");
        manifest.Name.ShouldBe("Test Theme");
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
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldReturnNull_WhenMissingThemeId()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage("", "Test Theme", "1.0.0");

        // Act
        var manifest = await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Assert
        manifest.ShouldBeNull();
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldLoadBaseTerrain()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithBaseTerrain("test-theme");

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var variants = _sut.GetAvailableVariants("test-theme", TerrainAssetType.Base, "base");

        // Assert
        variants.Count.ShouldBe(1);
        variants.ShouldContain(0);
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldLoadTerrainOverlays()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithOverlays("test-theme");

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var lightwoodsVariants = _sut.GetAvailableVariants("test-theme", TerrainAssetType.Overlay, "lightwoods");
        var heavyWoodsVariants = _sut.GetAvailableVariants("test-theme", TerrainAssetType.Overlay, "heavywoods");

        // Assert
        lightwoodsVariants.Count.ShouldBe(1);
        heavyWoodsVariants.Count.ShouldBe(1);
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldLoadEdgeImages()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithEdges("test-theme");

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var topEdgeVariants = _sut.GetAvailableVariants("test-theme", TerrainAssetType.EdgeTop, "0");
        var bottomEdgeVariants = _sut.GetAvailableVariants("test-theme", TerrainAssetType.EdgeBottom, "3");

        // Assert
        topEdgeVariants.Count.ShouldBe(1);
        bottomEdgeVariants.Count.ShouldBe(1);
    }

    [Fact]
    public async Task LoadTerrainFromMmtxStreamAsync_ShouldLoadMultipleVariants()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithMultipleVariants("test-theme");

        // Act
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var variants = _sut.GetAvailableVariants("test-theme", TerrainAssetType.Base, "base");

        // Assert
        variants.Count.ShouldBe(3);
        variants.ShouldContain(0);
        variants.ShouldContain(1);
        variants.ShouldContain(2);
    }
    
    [Fact]
    public async Task GetBaseTerrainImage_ShouldReturnImage_WhenLoaded()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithBaseTerrain("test-theme");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var image = await _sut.GetBaseTerrainImage("test-theme");

        // Assert
        image.ShouldNotBeNull();
        image.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GetBaseTerrainImage_ShouldReturnNull_WhenThemeNotLoaded()
    {
        // Act
        var image = await _sut.GetBaseTerrainImage("nonexistent-theme");

        // Assert
        image.ShouldBeNull();
    }
    
    [Fact]
    public async Task GetTerrainOverlayImage_ShouldReturnImage_WhenExists()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithOverlays("test-theme");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var image = await _sut.GetTerrainOverlayImage("test-theme", "lightwoods");

        // Assert
        image.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetTerrainOverlayImage_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithOverlays("test-theme");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var image = await _sut.GetTerrainOverlayImage("test-theme", "nonexistent");

        // Assert
        image.ShouldBeNull();
    }
    
    [Fact]
    public async Task GetEdgeImage_ShouldReturnImage_WhenExists()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithEdges("test-theme");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var coordinates = new HexCoordinates(0, 0);

        // Act
        var image = await _sut.GetEdgeImage("test-theme", HexDirection.Top, TerrainAssetType.EdgeTop, coordinates);

        // Assert
        image.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetEdgeImage_ShouldReturnNull_WhenInvalidEdgeType()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithEdges("test-theme");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var coordinates = new HexCoordinates(0, 0);

        // Act
        var image = await _sut.GetEdgeImage("test-theme", HexDirection.Top, TerrainAssetType.Base, coordinates);

        // Assert
        image.ShouldBeNull();
    }

    [Fact]
    public async Task GetEdgeImage_ShouldReturnConsistentVariant_ForSameCoordinates()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithMultipleEdgeVariants("test-theme");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);
        var coordinates = new HexCoordinates(5, 10);

        // Act - Get same edge twice
        var image1 = await _sut.GetEdgeImage("test-theme", HexDirection.Top, TerrainAssetType.EdgeTop, coordinates);
        var image2 = await _sut.GetEdgeImage("test-theme", HexDirection.Top, TerrainAssetType.EdgeTop, coordinates);

        // Assert - Should return same variant (deterministic)
        image1.ShouldBe(image2);
    }

    [Fact]
    public async Task GetEdgeImage_ShouldUseMultipleVariants_AcrossDifferentCoordinates()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithMultipleEdgeVariants("test-theme");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var distinctImages = new HashSet<string>();
        for (var i = 0; i < 32; i++)
        {
            var image = await _sut.GetEdgeImage(
                "test-theme",
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
    public void GetLoadedThemes_ShouldReturnEmpty_WhenNothingLoaded()
    {
        // Act
        var themes = _sut.GetLoadedThemes();

        // Assert
        themes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLoadedThemes_ShouldReturnLoadedThemes()
    {
        // Arrange
        using var mmtxStream1 = CreateMmtxPackage("theme1", "Theme 1", "1.0.0");
        using var mmtxStream2 = CreateMmtxPackage("theme2", "Theme 2", "1.0.0");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream1);
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream2);

        // Act
        var themes = _sut.GetLoadedThemes().ToList();

        // Assert
        themes.Count.ShouldBe(2);
        themes.ShouldContain("theme1");
        themes.ShouldContain("theme2");
    }
    
    [Fact]
    public async Task GetThemeManifest_ShouldReturnManifest_WhenLoaded()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackage("test-theme", "Test Theme", "2.0.0");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        var manifest = _sut.GetThemeManifest("test-theme");

        // Assert
        manifest.ShouldNotBeNull();
        manifest.Version.ShouldBe("2.0.0");
    }

    [Fact]
    public void GetThemeManifest_ShouldReturnNull_WhenNotLoaded()
    {
        // Act
        var manifest = _sut.GetThemeManifest("nonexistent");

        // Assert
        manifest.ShouldBeNull();
    }
    
    [Fact]
    public async Task ClearCache_ShouldClearAllData()
    {
        // Arrange
        using var mmtxStream = CreateMmtxPackageWithBaseTerrain("test-theme");
        await _sut.LoadTerrainFromMmtxStreamAsync(mmtxStream);

        // Act
        _sut.ClearCache();
        var themes = _sut.GetLoadedThemes();

        // Assert
        themes.ShouldBeEmpty();
    }

    private static MemoryStream CreateMmtxPackage(string themeId, string name, string version)
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
                    themeId,
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
            // Add a dummy file instead of manifest
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

    private static MemoryStream CreateMmtxPackageWithBaseTerrain(string themeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { themeId, name = "Test Theme", version = "1.0.0" };
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

    private static MemoryStream CreateMmtxPackageWithOverlays(string themeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { themeId, name = "Test Theme", version = "1.0.0" };
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
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithEdges(string themeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { themeId, name = "Test Theme", version = "1.0.0" };
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

    private static MemoryStream CreateMmtxPackageWithMultipleVariants(string themeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { themeId, name = "Test Theme", version = "1.0.0" };
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            // Add multiple base terrain variants
            for (int i = 1; i <= 3; i++)
            {
                var baseEntry = archive.CreateEntry($"base-{i}.png");
                using var baseStream = baseEntry.Open();
                baseStream.Write([0x89, 0x50, 0x4E, 0x47], 0, 4);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateMmtxPackageWithMultipleEdgeVariants(string themeId)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                var manifest = new { themeId, name = "Test Theme", version = "1.0.0" };
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
}
