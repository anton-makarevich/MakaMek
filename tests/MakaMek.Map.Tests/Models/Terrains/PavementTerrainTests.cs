using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class PavementTerrainTests
{
    [Fact]
    public void Height_Returns0()
    {
        var terrain = new PavementTerrain();
        terrain.Height.ShouldBe(0);
    }

    [Fact]
    public void MovementCost_Returns1()
    {
        var terrain = new PavementTerrain();
        terrain.MovementCost.ShouldBe(1);
    }

    [Fact]
    public void Id_ReturnsPavement()
    {
        var terrain = new PavementTerrain();
        terrain.Id.ShouldBe(MakaMekTerrains.Pavement);
    }

    [Fact]
    public void InterveningFactor_IsZero()
    {
        var terrain = new PavementTerrain();
        terrain.InterveningFactor.ShouldBe(0);
    }
}
