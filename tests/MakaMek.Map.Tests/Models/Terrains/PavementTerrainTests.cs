using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class PavementTerrainTests
{
    [Fact]
    public void Height_Returns0()
    {
        var sut = new PavementTerrain();
        sut.Height.ShouldBe(0);
    }

    [Fact]
    public void MovementCost_Returns1()
    {
        var sut = new PavementTerrain();
        sut.MovementCost.ShouldBe(1);
    }

    [Fact]
    public void Id_ReturnsPavement()
    {
        var sut = new PavementTerrain();
        sut.Id.ShouldBe(MakaMekTerrains.Pavement);
    }

    [Fact]
    public void InterveningFactor_IsZero()
    {
        var sut = new PavementTerrain();
        sut.InterveningFactor.ShouldBe(0);
    }
}
