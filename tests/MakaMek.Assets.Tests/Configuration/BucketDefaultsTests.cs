using Shouldly;
using Sanet.MakaMek.Assets.Configuration;

namespace Sanet.MakaMek.Assets.Tests.Configuration;

/// <summary>
/// Guards the default data bucket URL against silent drift: the origin is duplicated in the
/// data-release pipeline configuration (the DATA_BUCKET_BASE_URL repository variable and the
/// Pulumi custom domain), and a typo here would break asset loading for every client.
/// </summary>
public class BucketDefaultsTests
{
    private const string CanonicalBucketUrl = "https://data.makamek.nl";

    [Fact]
    public void BaseUrl_InBuildsWithoutAnEmbeddedUrl_FallsBackToTheCanonicalPublicOrigin()
    {
        // Tests build without -p:DataBucketBaseUrl, so the generated constant must stay null and
        // BaseUrl must resolve to the public bucket used by dev builds.
        BucketDefaults.BuildTimeBaseUrl.ShouldBeNull(
            "a non-null value means the test build embeds a release bucket URL; run tests without DataBucketBaseUrl");

        BucketDefaults.BaseUrl.ShouldBe(CanonicalBucketUrl);
    }

    [Theory]
    [InlineData("https://data.makamek.nl", CanonicalBucketUrl)]
    [InlineData("  https://data.makamek.nl  ", CanonicalBucketUrl)]
    [InlineData("\thttps://data.makamek.nl\n", CanonicalBucketUrl)]
    [InlineData("https://new-bucket.makamek.nl", "https://new-bucket.makamek.nl")]
    public void Resolve_TrimsWhitespaceFromTheBuildTimeUrl(string buildTimeUrl, string expected)
    {
        BucketDefaults.Resolve(buildTimeUrl).ShouldBe(expected);
    }

    [Fact]
    public void Resolve_WhenNoBuildTimeUrlIsEmbedded_ReturnsTheCanonicalOrigin()
    {
        BucketDefaults.Resolve(null).ShouldBe(CanonicalBucketUrl);
    }

    [Fact]
    public void BaseUrl_IsAnAbsoluteHttpsOrigin()
    {
        var baseUrl = BucketDefaults.BaseUrl;

        baseUrl.ShouldBe(baseUrl.Trim());
        baseUrl.ShouldNotEndWith("/");

        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri).ShouldBeTrue($"{baseUrl} must be an absolute URL");
        uri.ShouldNotBeNull();
        uri.Scheme.ShouldBe("https");
        uri.Host.ShouldBe("data.makamek.nl");
    }

    [Theory]
    [InlineData("units/manifest.json")]
    [InlineData("hexes/manifest.json")]
    public void BaseUrl_ComposesManifestUrlsWithoutDoubleSlashes(string manifestPath)
    {
        // Mirrors how the stream providers compose the listing URL from the configured base URL.
        var manifestUrl = $"{BucketDefaults.BaseUrl.TrimEnd('/')}/{manifestPath}";

        manifestUrl.ShouldBe($"https://data.makamek.nl/{manifestPath}");
    }
}
