using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class TerrainTests
{
    [Theory]
    [InlineData(MakaMekTerrains.Clear, typeof(ClearTerrain))]
    [InlineData(MakaMekTerrains.LightWoods, typeof(LightWoodsTerrain))]
    [InlineData(MakaMekTerrains.HeavyWoods, typeof(HeavyWoodsTerrain))]
    [InlineData(MakaMekTerrains.Rough, typeof(RoughTerrain))]
    [InlineData(MakaMekTerrains.Water, typeof(WaterTerrain))]
    [InlineData(MakaMekTerrains.Road, typeof(RoadTerrain))]
    [InlineData(MakaMekTerrains.Pavement, typeof(PavementTerrain))]
    [InlineData(MakaMekTerrains.Bridge, typeof(BridgeTerrain))]
    [InlineData(MakaMekTerrains.Rubble, typeof(RubbleTerrain))]
    public void CreateTerrainOfType_KnownTypes_ReturnsCorrectConcreteType(MakaMekTerrains terrainType, Type expectedType)
    {
        var terrain = Terrain.CreateTerrainOfType(terrainType);

        terrain.ShouldBeOfType(expectedType);
        terrain.Id.ShouldBe(terrainType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-3)]
    public void CreateTerrainOfType_WaterWithHeight_ReturnsWaterTerrainWithCorrectDepth(int depth)
    {
        var terrain = Terrain.CreateTerrainOfType(MakaMekTerrains.Water, depth);

        terrain.ShouldBeOfType<WaterTerrain>();
        terrain.Height.ShouldBe(depth);
    }

    [Fact]
    public void CreateTerrainOfType_WaterWithoutHeight_DefaultsToDepthZero()
    {
        var terrain = Terrain.CreateTerrainOfType(MakaMekTerrains.Water);

        terrain.ShouldBeOfType<WaterTerrain>();
        terrain.Height.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 50)]
    [InlineData(3, 100)]
    public void CreateTerrainOfType_BridgeWithParameters_ReturnsBridgeWithCorrectValues(int height, int constructionFactor)
    {
        var terrain = Terrain.CreateTerrainOfType(MakaMekTerrains.Bridge, height, constructionFactor);

        terrain.ShouldBeOfType<BridgeTerrain>();
        terrain.Height.ShouldBe(height);
    }

    [Fact]
    public void CreateTerrainOfType_BridgeWithoutParameters_DefaultsToHeightZeroAndConstructionFactor40()
    {
        var terrain = Terrain.CreateTerrainOfType(MakaMekTerrains.Bridge);

        terrain.ShouldBeOfType<BridgeTerrain>();
        terrain.Height.ShouldBe(0);
    }

    [Fact]
    public void CreateTerrainOfType_UnknownType_ThrowsArgumentException()
    {
        var unknownType = (MakaMekTerrains)999;

        Should.Throw<ArgumentException>(() => Terrain.CreateTerrainOfType(unknownType))
            .Message.ShouldContain("Unknown terrain type");
    }

    [Fact]
    public void FromData_WithTerrainData_DelegatesToCreateTerrainOfType()
    {
        var data = new TerrainData { Type = MakaMekTerrains.Clear };

        var terrain = Terrain.FromData(data);

        terrain.ShouldBeOfType<ClearTerrain>();
        terrain.Id.ShouldBe(MakaMekTerrains.Clear);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void FromData_WithWaterData_DelegatesWithHeight(int depth)
    {
        var data = new TerrainData { Type = MakaMekTerrains.Water, Height = depth };

        var terrain = Terrain.FromData(data);

        terrain.ShouldBeOfType<WaterTerrain>();
        terrain.Height.ShouldBe(depth);
    }

    [Theory]
    [InlineData(1, 50)]
    [InlineData(3, 100)]
    public void FromData_WithBridgeData_DelegatesWithHeightAndConstructionFactor(int height, int constructionFactor)
    {
        var data = new TerrainData
        {
            Type = MakaMekTerrains.Bridge,
            Height = height,
            ConstructionFactor = constructionFactor
        };

        var terrain = Terrain.FromData(data);

        terrain.ShouldBeOfType<BridgeTerrain>();
        terrain.Height.ShouldBe(height);
    }
}
