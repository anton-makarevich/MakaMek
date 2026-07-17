using Sanet.MakaMek.Services.ResourceProviders;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Services.ResourceProviders;

public class LocalFolderResourceStreamProviderTests : IDisposable
{
    private readonly string _testFolder;

    public LocalFolderResourceStreamProviderTests()
    {
        _testFolder = Path.Combine(Path.GetTempPath(), $"MakaMek_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testFolder))
        {
            Directory.Delete(_testFolder, true);
        }
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenFolderIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new LocalFolderResourceStreamProvider(null!, "mmux"));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenExtensionIsNull()
    {
        Should.Throw<ArgumentNullException>(() =>
            new LocalFolderResourceStreamProvider(_testFolder, null!));
    }

    [Fact]
    public async Task GetAvailableResourceIds_ReturnsEmpty_WhenFolderDoesNotExist()
    {
        var sut = new LocalFolderResourceStreamProvider(
            Path.Combine(_testFolder, "nonexistent"), "mmux");

        var result = await sut.GetAvailableResourceIds();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAvailableResourceIds_ReturnsEmpty_WhenNoMatchingFiles()
    {
        File.WriteAllText(Path.Combine(_testFolder, "readme.txt"), "hello");
        var sut = new LocalFolderResourceStreamProvider(_testFolder, "mmux");

        var result = await sut.GetAvailableResourceIds();

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAvailableResourceIds_ReturnsMatchingFiles()
    {
        var expected1 = Path.Combine(_testFolder, "Atlas.mmux");
        var expected2 = Path.Combine(_testFolder, "Thunderbolt.mmux");
        File.WriteAllText(expected1, "data1");
        File.WriteAllText(expected2, "data2");
        File.WriteAllText(Path.Combine(_testFolder, "Grasshopper.mmtx"), "other");

        var sut = new LocalFolderResourceStreamProvider(_testFolder, "mmux");

        var result = (await sut.GetAvailableResourceIds()).ToList();

        result.Count.ShouldBe(2);
        result.ShouldContain(expected1);
        result.ShouldContain(expected2);
    }

    [Fact]
    public async Task GetAvailableResourceIds_IgnoresSubdirectories()
    {
        File.WriteAllText(Path.Combine(_testFolder, "Atlas.mmux"), "data");
        var subDir = Path.Combine(_testFolder, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "Hidden.mmux"), "hidden");

        var sut = new LocalFolderResourceStreamProvider(_testFolder, "mmux");

        var result = (await sut.GetAvailableResourceIds()).ToList();

        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetResourceStream_ReturnsNull_WhenResourceIdIsNull()
    {
        var sut = new LocalFolderResourceStreamProvider(_testFolder, "mmux");

        var result = await sut.GetResourceStream(null!);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResourceStream_ReturnsNull_WhenResourceIdIsEmpty()
    {
        var sut = new LocalFolderResourceStreamProvider(_testFolder, "mmux");

        var result = await sut.GetResourceStream(string.Empty);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResourceStream_ReturnsNull_WhenFileDoesNotExist()
    {
        var sut = new LocalFolderResourceStreamProvider(_testFolder, "mmux");

        var result = await sut.GetResourceStream(Path.Combine(_testFolder, "Nonexistent.mmux"));

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetResourceStream_ReturnsStream_WithCorrectContent()
    {
        var filePath = Path.Combine(_testFolder, "Atlas.mmux");
        var expectedContent = "mech data payload";
        await File.WriteAllTextAsync(filePath, expectedContent);

        var sut = new LocalFolderResourceStreamProvider(_testFolder, "mmux");

        await using var stream = await sut.GetResourceStream(filePath);
        stream.ShouldNotBeNull();

        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        content.ShouldBe(expectedContent);
    }

    [Fact]
    public async Task GetAvailableResourceIds_CaseInsensitiveExtensionMatch()
    {
        File.WriteAllText(Path.Combine(_testFolder, "Atlas.MMUX"), "data");

        var sut = new LocalFolderResourceStreamProvider(_testFolder, "mmux");

        var result = (await sut.GetAvailableResourceIds()).ToList();

        result.Count.ShouldBe(1);
    }
}
