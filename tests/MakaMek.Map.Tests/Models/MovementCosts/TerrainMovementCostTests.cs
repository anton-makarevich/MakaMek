using NSubstitute;
using Sanet.MakaMek.Localization;
using Sanet.MakaMek.Map.Models.MovementCosts;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.MovementCosts;

public class TerrainMovementCostTests
{
    [Fact]
    public void Render_NonWaterTerrain_UsesLocalizedTerrainName()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Terrain").Returns("entered {0}, {1} MP");
        localization.GetString("Terrain_LightWoods").Returns("Light Woods");
        var sut = new TerrainMovementCost { TerrainId = MakaMekTerrains.LightWoods, Value = 1 };

        var result = sut.Render(localization);

        result.ShouldBe("entered Light Woods, 1 MP");
    }

    [Fact]
    public void Render_WaterWithoutDepth_UsesGenericFormat()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Terrain").Returns("entered {0}, {1} MP");
        localization.GetString("Terrain_Water").Returns("Water");
        var sut = new TerrainMovementCost { TerrainId = MakaMekTerrains.Water, Value = 2 };

        var result = sut.Render(localization);

        result.ShouldBe("entered Water, 2 MP");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void Render_WaterWithVariousDepths_IncludesDepth(int depth)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Terrain_Water").Returns("entered {0} (depth {1}), {2} MP");
        localization.GetString("Terrain_Water").Returns("Water");
        var sut = new TerrainMovementCost { TerrainId = MakaMekTerrains.Water, Value = 2, Depth = depth };

        var result = sut.Render(localization);

        result.ShouldBe($"entered Water (depth {depth}), 2 MP");
    }

    [Fact]
    public void Render_ClearTerrain_UsesLocalizedClearName()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.GetString("MovementCost_Terrain").Returns("entered {0}, {1} MP");
        localization.GetString("Terrain_Clear").Returns("Clear");
        var sut = new TerrainMovementCost { TerrainId = MakaMekTerrains.Clear, Value = 1 };

        var result = sut.Render(localization);

        result.ShouldBe("entered Clear, 1 MP");
    }
}
