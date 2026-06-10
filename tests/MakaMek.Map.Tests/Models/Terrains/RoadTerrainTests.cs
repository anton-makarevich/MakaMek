using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class RoadTerrainTests
{
    [Fact]
    public void Height_Returns0()
    {
        var sut = new RoadTerrain();
        sut.Height.ShouldBe(0);
    }

    [Fact]
    public void MovementCost_Returns0()
    {
        var sut = new RoadTerrain();
        sut.MovementCost.ShouldBe(0);
    }

    [Fact]
    public void Id_ReturnsRoad()
    {
        var sut = new RoadTerrain();
        sut.Id.ShouldBe(MakaMekTerrains.Road);
    }

    [Fact]
    public void InterveningFactor_IsZero()
    {
        var sut = new RoadTerrain();
        sut.InterveningFactor.ShouldBe(0);
    }
}
