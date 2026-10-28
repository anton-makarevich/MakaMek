using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sanet.MakaMek.Assets.Models.Units;
using Sanet.MakaMek.Assets.Services;
using Sanet.MakaMek.Avalonia.Services;
using Shouldly;

namespace MakaMek.Avalonia.Tests.Services;

/// <summary>
/// The sprite atlas is an optional extra on top of the static image: a failed lookup must never stop
/// the unit from rendering, and the failure must not be cached so a provider reload can recover.
/// </summary>
public class UnitSpriteServiceTests
{
    private readonly IUnitCachingService _unitCachingService = Substitute.For<IUnitCachingService>();
    private readonly UnitSpriteService _sut;

    public UnitSpriteServiceTests()
    {
        _sut = new UnitSpriteService(_unitCachingService, NullLogger<UnitSpriteService>.Instance);
    }

    [Fact]
    public async Task GetSpriteSet_ReturnsNull_WhenTheAnimationLookupThrows()
    {
        _unitCachingService.GetUnitAnimation(Arg.Any<string>())
            .Returns<Task<UnitAnimationData?>>(_ => throw new InvalidOperationException("Package cache failed"));

        var result = await _sut.GetSpriteSet("AS7-D");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetSpriteSet_DoesNotCacheTheFailure()
    {
        _unitCachingService.GetUnitAnimation(Arg.Any<string>())
            .Returns<Task<UnitAnimationData?>>(_ => throw new InvalidOperationException("Package cache failed"));

        await _sut.GetSpriteSet("AS7-D");
        await _sut.GetSpriteSet("AS7-D");

        await _unitCachingService.Received(2).GetUnitAnimation("AS7-D");
    }

    [Fact]
    public async Task GetSpriteSet_ReturnsNull_WhenTheUnitHasNoSpritePackage()
    {
        _unitCachingService.GetUnitAnimation("AS7-D")
            .Returns(Task.FromResult<UnitAnimationData?>(null));

        var result = await _sut.GetSpriteSet("AS7-D");

        result.ShouldBeNull();
    }
}
