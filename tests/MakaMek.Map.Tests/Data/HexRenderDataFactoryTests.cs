using NSubstitute;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Services;
using Shouldly;
using System.Collections.Generic;

namespace Sanet.MakaMek.Map.Tests.Data;

public class HexRenderDataFactoryTests
{
    private readonly IBattleMap _map = Substitute.For<IBattleMap>();
    private readonly ITerrainBitmaskService _bitmaskService = Substitute.For<ITerrainBitmaskService>();

    // ─── Create: edges ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_ReturnsEdgesFromMap()
    {
        var coord = new HexCoordinates(2, 2);
        var hex = new Hex(coord);
        var edges = new List<HexEdge> { new(coord, HexDirection.Top, 1) };
        _map.GetHex(coord).Returns(hex);
        _map.GetHexEdges(coord).Returns(edges);

        var result = HexRenderDataFactory.Create(_map, coord, _bitmaskService);

        result.Edges.ShouldBe(edges);
    }

    [Fact]
    public void Create_NoWaterTerrain_WaterBitmaskIsNull()
    {
        var coord = new HexCoordinates(2, 2);
        var hex = new Hex(coord);
        _map.GetHex(coord).Returns(hex);
        _map.GetHexEdges(coord).Returns(Array.Empty<HexEdge>());

        var result = HexRenderDataFactory.Create(_map, coord, _bitmaskService);

        result.WaterBitmask.ShouldBeNull();
    }

    // ─── Create: water bitmask ───────────────────────────────────────────────────

    [Fact]
    public void Create_WithWaterTerrain_ComputesCanonicalWaterBitmask()
    {
        var coord = new HexCoordinates(2, 2);
        var hex = new Hex(coord);
        hex.AddTerrain(new WaterTerrain());
        _map.GetHex(coord).Returns(hex);
        _map.GetHexEdges(coord).Returns(Array.Empty<HexEdge>());

        var expected = new CanonicalBitmaskResult(0b000011, 0);
        _bitmaskService
            .ComputeCanonicalBitmask(_map, coord, MakaMekTerrains.Water, null)
            .Returns(expected);

        var result = HexRenderDataFactory.Create(_map, coord, _bitmaskService);

        result.WaterBitmask.ShouldBe(expected);
    }

    // ─── Create: road bitmask ────────────────────────────────────────────────────

    [Fact]
    public void Create_NoRoadOrBridge_RoadBitmaskIsNull()
    {
        var coord = new HexCoordinates(2, 2);
        var hex = new Hex(coord);
        _map.GetHex(coord).Returns(hex);
        _map.GetHexEdges(coord).Returns(Array.Empty<HexEdge>());

        var result = HexRenderDataFactory.Create(_map, coord, _bitmaskService);

        result.RoadBitmask.ShouldBeNull();
    }

    [Fact]
    public void Create_WithRoadTerrain_ComputesRoadBitmask()
    {
        var coord = new HexCoordinates(2, 2);
        var hex = new Hex(coord);
        hex.AddTerrain(new RoadTerrain());
        _map.GetHex(coord).Returns(hex);
        _map.GetHexEdges(coord).Returns(Array.Empty<HexEdge>());

        _bitmaskService
            .ComputeRawBitmask(_map, coord, MakaMekTerrains.Road, Arg.Any<Func<Hex, Hex, bool>?>())
            .Returns((byte)0b000001);
        _bitmaskService
            .ComputeRawBitmask(_map, coord, MakaMekTerrains.Bridge, Arg.Any<Func<Hex, Hex, bool>?>())
            .Returns((byte)0b000000);

        var expected = new CanonicalBitmaskResult(0b000001, 0);
        _bitmaskService.CanonicalizeRawMask(0b000001).Returns(expected);

        var result = HexRenderDataFactory.Create(_map, coord, _bitmaskService);

        result.RoadBitmask.ShouldBe(expected);
    }

    [Fact]
    public void Create_WithBridgeTerrain_ComputesRoadBitmask()
    {
        var coord = new HexCoordinates(2, 2);
        var hex = new Hex(coord);
        hex.AddTerrain(new BridgeTerrain());
        _map.GetHex(coord).Returns(hex);
        _map.GetHexEdges(coord).Returns(Array.Empty<HexEdge>());

        _bitmaskService
            .ComputeRawBitmask(_map, coord, MakaMekTerrains.Road, Arg.Any<Func<Hex, Hex, bool>?>())
            .Returns((byte)0b000000);
        _bitmaskService
            .ComputeRawBitmask(_map, coord, MakaMekTerrains.Bridge, Arg.Any<Func<Hex, Hex, bool>?>())
            .Returns((byte)0b000011);

        var expected = new CanonicalBitmaskResult(0b000011, 0);
        _bitmaskService.CanonicalizeRawMask(0b000011).Returns(expected);

        var result = HexRenderDataFactory.Create(_map, coord, _bitmaskService);

        result.RoadBitmask.ShouldBe(expected);
    }

    [Fact]
    public void Create_HexNotOnMap_ThrowsArgumentException()
    {
        var coord = new HexCoordinates(99, 99);
        _map.GetHex(coord).Returns((Hex?)null);

        Should.Throw<ArgumentException>(() =>
            HexRenderDataFactory.Create(_map, coord, _bitmaskService));
    }

    // ─── GetAffectedCoordinates ──────────────────────────────────────────────────

    [Fact]
    public void GetAffectedCoordinates_IncludesChangedHexWhenOnMap()
    {
        var coord = new HexCoordinates(3, 3);
        _map.IsOnMap(coord).Returns(true);
        foreach (var n in coord.GetAllNeighbours())
            _map.IsOnMap(n).Returns(false);

        var result = HexRenderDataFactory.GetAffectedCoordinates(coord, _map).ToList();

        result.ShouldContain(coord);
    }

    [Fact]
    public void GetAffectedCoordinates_ExcludesChangedHexWhenOffMap()
    {
        var coord = new HexCoordinates(99, 99);
        _map.IsOnMap(coord).Returns(false);
        foreach (var n in coord.GetAllNeighbours())
            _map.IsOnMap(n).Returns(false);

        var result = HexRenderDataFactory.GetAffectedCoordinates(coord, _map).ToList();

        result.ShouldNotContain(coord);
    }

    [Fact]
    public void GetAffectedCoordinates_IncludesOnMapNeighbors()
    {
        var coord = new HexCoordinates(3, 3);
        var onMapNeighbor = coord.GetNeighbour(HexDirection.Top);
        _map.IsOnMap(coord).Returns(true);
        foreach (var n in coord.GetAllNeighbours())
            _map.IsOnMap(n).Returns(n == onMapNeighbor);

        var result = HexRenderDataFactory.GetAffectedCoordinates(coord, _map).ToList();

        result.ShouldContain(onMapNeighbor);
    }

    [Fact]
    public void GetAffectedCoordinates_ExcludesOffMapNeighbors()
    {
        var coord = new HexCoordinates(1, 1);
        _map.IsOnMap(coord).Returns(true);
        var neighbors = coord.GetAllNeighbours().ToList();
        // Only the first neighbor is on the map
        _map.IsOnMap(neighbors[0]).Returns(true);
        for (var i = 1; i < neighbors.Count; i++)
            _map.IsOnMap(neighbors[i]).Returns(false);

        var result = HexRenderDataFactory.GetAffectedCoordinates(coord, _map).ToList();

        for (var i = 1; i < neighbors.Count; i++)
            result.ShouldNotContain(neighbors[i]);
    }
}
