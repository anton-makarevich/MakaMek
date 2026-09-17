using Sanet.MakaMek.Bots.Services;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Services;

public class HexFilteringServiceTests
{
    private readonly HexFilteringService _sut = new();

    [Fact]
    public void GetPriorityFacings_ShouldReturnPrimaryAndAdjacentFlanks()
    {
        var result = _sut.GetPriorityFacings(
            new HexCoordinates(5, 5),
            [new HexCoordinates(5, 1)]);

        result.Count.ShouldBe(3);
        result.ShouldBe([HexDirection.Top, HexDirection.TopLeft, HexDirection.TopRight]);
        result.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void GetPriorityFacings_ShouldAimAtEnemyCenterOfMass()
    {
        var candidate = new HexCoordinates(5, 5);
        var topRight = candidate.GetNeighbour(HexDirection.TopRight);
        var result = _sut.GetPriorityFacings(
            candidate,
            [topRight, topRight]);

        result[0].ShouldBe(HexDirection.TopRight);
    }

    [Fact]
    public void GetPriorityFacings_ShouldUseStableFallback_WhenNoEnemiesAreKnown()
    {
        var result = _sut.GetPriorityFacings(new HexCoordinates(5, 5), null);

        result.ShouldBe([HexDirection.Top, HexDirection.TopLeft, HexDirection.TopRight]);
    }
}
