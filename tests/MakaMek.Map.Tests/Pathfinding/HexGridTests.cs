using Sanet.MakaMek.Map.Exceptions;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Pathfinding;

public class HexGridTests
{
    [Fact]
    public void AddHex_StoresHexAndStampsBiome()
    {
        var sut = new HexGrid(2, 2, "test.biome");
        var hex = new Hex(new HexCoordinates(1, 2));

        sut.AddHex(hex);

        sut.GetHex(new HexCoordinates(1, 2)).ShouldBeSameAs(hex);
        hex.Biome.ShouldBe("test.biome");
    }

    [Fact]
    public void GetHex_MissingCoordinates_ReturnsNull()
    {
        var sut = TraversalTestMap.BuildClearGrid(2, 2);

        sut.GetHex(new HexCoordinates(3, 1)).ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(4, 1)]
    [InlineData(1, 3)]
    public void AddHex_OutsideBoundaries_Throws(int q, int r)
    {
        var sut = new HexGrid(3, 2, "test.biome");
        var hex = new Hex(new HexCoordinates(q, r));

        var exception = Should.Throw<HexOutsideOfMapBoundariesException>(() => sut.AddHex(hex));

        exception.Coordinates.ShouldBe(hex.Coordinates);
        exception.MapWidth.ShouldBe(3);
        exception.MapHeight.ShouldBe(2);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(3, 2, true)]
    [InlineData(0, 1, false)]
    [InlineData(4, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 3, false)]
    public void IsOnMap_UsesOneBasedBounds(int q, int r, bool expected)
    {
        var sut = new HexGrid(3, 2, "test.biome");

        sut.IsOnMap(new HexCoordinates(q, r)).ShouldBe(expected);
    }

    [Fact]
    public void GetHexes_ReturnsEveryAddedHex()
    {
        var sut = new HexGrid(2, 2, "test.biome");
        sut.AddHex(new Hex(new HexCoordinates(1, 1)));
        sut.AddHex(new Hex(new HexCoordinates(2, 2)));

        sut.GetHexes().Select(h => h.Coordinates).ShouldBe(
            [new HexCoordinates(1, 1), new HexCoordinates(2, 2)]);
    }

    [Fact]
    public void GetAffectedCoordinates_ReturnsHexAndOnMapNeighbours()
    {
        var sut = new HexGrid(2, 2, "test.biome");
        var centre = new HexCoordinates(1, 1);

        var affected = sut.GetAffectedCoordinates(centre).ToList();

        affected[0].ShouldBe(centre);
        affected.ShouldAllBe(c => sut.IsOnMap(c));
        affected.ShouldContain(centre);
        affected.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void GetAffectedCoordinates_OffMapCentre_ReturnsOnlyOnMapNeighbours()
    {
        var sut = new HexGrid(1, 1, "test.biome");

        sut.GetAffectedCoordinates(new HexCoordinates(3, 3)).ShouldBeEmpty();
    }
}
