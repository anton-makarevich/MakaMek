using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class RoadTerrainTests
{
    [Fact]
    public void Height_Returns0()
    {
        var terrain = new RoadTerrain();
        terrain.Height.ShouldBe(0);
    }

    [Fact]
    public void MovementCost_Returns1()
    {
        var terrain = new RoadTerrain();
        terrain.MovementCost.ShouldBe(1);
    }

    [Fact]
    public void Id_ReturnsRoad()
    {
        var terrain = new RoadTerrain();
        terrain.Id.ShouldBe(MakaMekTerrains.Road);
    }

    [Fact]
    public void InterveningFactor_IsZero()
    {
        var terrain = new RoadTerrain();
        terrain.InterveningFactor.ShouldBe(0);
    }
}
