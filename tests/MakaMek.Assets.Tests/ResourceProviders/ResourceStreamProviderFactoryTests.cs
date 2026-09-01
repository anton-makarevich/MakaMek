using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sanet.MakaMek.Assets.Configuration;
using Sanet.MakaMek.Assets.ResourceProviders;
using Sanet.MakaMek.Services;
using Shouldly;

namespace Sanet.MakaMek.Assets.Tests.ResourceProviders;

public class ResourceStreamProviderFactoryTests
{
    private readonly IFileCachingService _cachingService = Substitute.For<IFileCachingService>();
    private readonly ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    private ResourceStreamProviderFactory CreateSut() => new(_cachingService, _loggerFactory);

    private static object? GetPrivateField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.ShouldNotBeNull($"Expected private field '{fieldName}' on {instance.GetType().Name}.");
        return field.GetValue(instance);
    }

    [Fact]
    public void Create_BucketConfig_ReturnsBucketResourceStreamProvider()
    {
        var sut = CreateSut();
        var config = new AssetProviderConfigData(
            "bucket", ProviderType.Bucket, AssetType.Units, "https://data.example.com", IsActive: true, IsDefault: true, SortOrder: 0);

        var provider = sut.Create(config);

        provider.ShouldBeOfType<BucketResourceStreamProvider>();
    }

    [Fact]
    public void Create_BucketUnits_UsesMmuxExtensionAndUnitsManifest()
    {
        var sut = CreateSut();
        var config = new AssetProviderConfigData(
            "bucket", ProviderType.Bucket, AssetType.Units, "https://data.example.com", IsActive: true, IsDefault: true, SortOrder: 0);

        var provider = sut.Create(config);

        GetPrivateField(provider, "_fileExtension").ShouldBe("mmux");
        GetPrivateField(provider, "_manifestUrl").ShouldBe("https://data.example.com/units/manifest.json");
    }

    [Fact]
    public void Create_BucketHexes_UsesMmtxExtensionAndHexesManifest()
    {
        var sut = CreateSut();
        var config = new AssetProviderConfigData(
            "bucket", ProviderType.Bucket, AssetType.Hexes, "https://data.example.com/", IsActive: true, IsDefault: true, SortOrder: 0);

        var provider = sut.Create(config);

        GetPrivateField(provider, "_fileExtension").ShouldBe("mmtx");
        GetPrivateField(provider, "_manifestUrl").ShouldBe("https://data.example.com/hexes/manifest.json");
    }

    [Fact]
    public void Create_GitHubConfig_ReturnsGitHubResourceStreamProvider()
    {
        var sut = CreateSut();
        var config = new AssetProviderConfigData(
            "github", ProviderType.GitHub, AssetType.Units, "https://api.github.com/units", IsActive: true, IsDefault: true, SortOrder: 1);

        var provider = sut.Create(config);

        provider.ShouldBeOfType<GitHubResourceStreamProvider>();
    }

    [Fact]
    public void Create_GitHubUnits_UsesMmuxExtensionAndApiUrl()
    {
        var sut = CreateSut();
        var config = new AssetProviderConfigData(
            "github", ProviderType.GitHub, AssetType.Units, "https://api.github.com/units", IsActive: true, IsDefault: true, SortOrder: 1);

        var provider = sut.Create(config);

        GetPrivateField(provider, "_fileExtension").ShouldBe("mmux");
        GetPrivateField(provider, "_apiUrl").ShouldBe("https://api.github.com/units");
    }

    [Fact]
    public void Create_GitHubHexes_UsesMmtxExtension()
    {
        var sut = CreateSut();
        var config = new AssetProviderConfigData(
            "github", ProviderType.GitHub, AssetType.Hexes, "https://api.github.com/hexes", IsActive: true, IsDefault: true, SortOrder: 1);

        var provider = sut.Create(config);

        GetPrivateField(provider, "_fileExtension").ShouldBe("mmtx");
    }

    [Fact]
    public void Create_FilesystemConfig_ReturnsLocalFolderResourceStreamProvider()
    {
        var sut = CreateSut();
        var config = new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Units, "C:\\assets\\units", IsActive: true, IsDefault: false, SortOrder: 2);

        var provider = sut.Create(config);

        provider.ShouldBeOfType<LocalFolderResourceStreamProvider>();
    }

    [Fact]
    public void Create_FilesystemUnits_UsesFolderPathAndMmuxExtension()
    {
        var sut = CreateSut();
        var config = new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Units, "C:\\assets\\units", IsActive: true, IsDefault: false, SortOrder: 2);

        var provider = sut.Create(config);

        GetPrivateField(provider, "_folderPath").ShouldBe("C:\\assets\\units");
        GetPrivateField(provider, "_fileExtension").ShouldBe("mmux");
    }

    [Fact]
    public void Create_FilesystemHexes_UsesFolderPathAndMmtxExtension()
    {
        var sut = CreateSut();
        var config = new AssetProviderConfigData(
            "local", ProviderType.Filesystem, AssetType.Hexes, "C:\\assets\\hexes", IsActive: true, IsDefault: false, SortOrder: 2);

        var provider = sut.Create(config);

        GetPrivateField(provider, "_folderPath").ShouldBe("C:\\assets\\hexes");
        GetPrivateField(provider, "_fileExtension").ShouldBe("mmtx");
    }

    [Fact]
    public void CreateAll_CreatesProviderForEachConfig_InOrder()
    {
        var sut = CreateSut();
        var configs = new[]
        {
            new AssetProviderConfigData("bucket", ProviderType.Bucket, AssetType.Units, "https://data.example.com", IsActive: true, IsDefault: true, SortOrder: 0),
            new AssetProviderConfigData("local", ProviderType.Filesystem, AssetType.Hexes, "C:\\assets", IsActive: true, IsDefault: false, SortOrder: 1)
        };

        var providers = sut.CreateAll(configs);

        providers.Count.ShouldBe(2);
        providers[0].ShouldBeOfType<BucketResourceStreamProvider>();
        providers[1].ShouldBeOfType<LocalFolderResourceStreamProvider>();
    }

    [Fact]
    public void Create_WithNullConfig_ThrowsArgumentNullException()
    {
        var sut = CreateSut();

        var exception = Should.Throw<ArgumentNullException>(() => sut.Create(null!));

        exception.ParamName.ShouldBe("config");
    }
}
