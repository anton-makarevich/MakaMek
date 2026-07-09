using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Data;

public class HexRenderDataTests
{
    [Fact]
    public void Constructor_SetsPropertiesCorrectly()
    {
        var coords = new HexCoordinates(3, 5);
        var hex = new Hex(coords, 2);
        var edges = new List<HexEdge>
        {
            new(coords, HexDirection.Top, 1),
            new(coords, HexDirection.BottomRight, -2),
        };
        var waterBitmask = new CanonicalBitmaskResult(0b001010, 2);
        var roadBitmask = new CanonicalBitmaskResult(0b100001, 0);

        var sut = new HexRenderData(hex, edges, waterBitmask, roadBitmask);

        sut.Hex.ShouldBeSameAs(hex);
        sut.Edges.ShouldBe(edges);
        sut.WaterBitmask.ShouldBeSameAs(waterBitmask);
        sut.RoadBitmask.ShouldBeSameAs(roadBitmask);
    }

    [Fact]
    public void Constructor_AllowsNullBitmasks()
    {
        var hex = new Hex(new HexCoordinates(0, 0));
        var edges = Array.Empty<HexEdge>();

        var sut = new HexRenderData(hex, edges, null, null);

        sut.Hex.ShouldBeSameAs(hex);
        sut.Edges.ShouldBeEmpty();
        sut.WaterBitmask.ShouldBeNull();
        sut.RoadBitmask.ShouldBeNull();
    }
}
