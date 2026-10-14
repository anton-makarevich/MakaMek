using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class DefaultMovementCostProviderTests
{
    private readonly DefaultMovementCostProvider _sut = new();

    [Theory]
    [InlineData(MakaMekTerrains.Clear, 0, 0)]
    [InlineData(MakaMekTerrains.LightWoods, 0, 1)]
    [InlineData(MakaMekTerrains.HeavyWoods, 0, 2)]
    [InlineData(MakaMekTerrains.Rough, 0, 1)]
    [InlineData(MakaMekTerrains.Water, 0, 0)]
    [InlineData(MakaMekTerrains.Water, -1, 1)]
    [InlineData(MakaMekTerrains.Water, -2, 3)]
    [InlineData(MakaMekTerrains.Road, 0, 0)]
    [InlineData(MakaMekTerrains.Pavement, 0, 0)]
    [InlineData(MakaMekTerrains.Bridge, 0, 0)]
    [InlineData(MakaMekTerrains.Rubble, 0, 1)]
    public void GetMovementCost_ShouldUseClassicTerrainCosts(MakaMekTerrains terrainType, int height, int expected)
    {
        _sut.GetMovementCost(terrainType, height).ShouldBe(expected);
    }

    [Fact]
    public void GetMovementCost_ShouldThrow_WhenTerrainTypeIsUnknown()
    {
        var unknownTerrain = (MakaMekTerrains)999;

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => _sut.GetMovementCost(unknownTerrain, 0));

        exception.ParamName.ShouldBe("terrainType");
    }

}
