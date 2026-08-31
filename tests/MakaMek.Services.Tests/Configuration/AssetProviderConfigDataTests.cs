using Shouldly;
using Sanet.MakaMek.Services.Configuration;

namespace Sanet.MakaMek.Services.Tests.Configuration;

public class AssetProviderConfigDataTests
{
    private static AssetProviderConfigData CreateProvider(
        string id = "p1",
        ProviderType providerType = ProviderType.Bucket,
        AssetType assetType = AssetType.Units,
        string urlOrPath = "https://data.makamek.nl/units/manifest.json",
        bool isActive = true,
        bool isDefault = false,
        int sortOrder = 0) =>
        new(id, providerType, assetType, urlOrPath, isActive, isDefault, sortOrder);

    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var provider = CreateProvider();

        provider.Id.ShouldBe("p1");
        provider.ProviderType.ShouldBe(ProviderType.Bucket);
        provider.AssetType.ShouldBe(AssetType.Units);
        provider.UrlOrPath.ShouldBe("https://data.makamek.nl/units/manifest.json");
        provider.IsActive.ShouldBeTrue();
        provider.IsDefault.ShouldBeFalse();
        provider.SortOrder.ShouldBe(0);
    }

    [Fact]
    public void Equality_TwoProvidersWithSameValues_AreEqual()
    {
        var provider1 = CreateProvider();
        var provider2 = CreateProvider();

        (provider1 == provider2).ShouldBeTrue();
        provider1.Equals(provider2).ShouldBeTrue();
        provider1.GetHashCode().ShouldBe(provider2.GetHashCode());
    }

    [Fact]
    public void Equality_ProvidersWithDifferentProperty_AreNotEqual()
    {
        var provider1 = CreateProvider(sortOrder: 0);
        var provider2 = CreateProvider(sortOrder: 5);

        (provider1 == provider2).ShouldBeFalse();
        provider1.Equals(provider2).ShouldBeFalse();
    }

    [Fact]
    public void Equality_ProvidersWithDifferentIsDefault_AreNotEqual()
    {
        var provider1 = CreateProvider(isDefault: false);
        var provider2 = CreateProvider(isDefault: true);

        (provider1 == provider2).ShouldBeFalse();
    }

    [Fact]
    public void WithExpression_CreatesModifiedCopy_KeepingOtherValues()
    {
        var provider = CreateProvider();

        var updated = provider with { SortOrder = 3 };

        updated.SortOrder.ShouldBe(3);
        updated.Id.ShouldBe(provider.Id);
        updated.ProviderType.ShouldBe(provider.ProviderType);
        updated.AssetType.ShouldBe(provider.AssetType);
        updated.UrlOrPath.ShouldBe(provider.UrlOrPath);
        updated.IsActive.ShouldBe(provider.IsActive);
        updated.IsDefault.ShouldBe(provider.IsDefault);
        provider.SortOrder.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsDefault_IsCorrectlyIdentified(bool isDefault)
    {
        var provider = CreateProvider(isDefault: isDefault);

        provider.IsDefault.ShouldBe(isDefault);
    }
}
