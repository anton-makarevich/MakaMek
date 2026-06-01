using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class BridgeTerrainTests
{
    [Fact]
    public void Id_ReturnsBridge()
    {
        var terrain = new BridgeTerrain();
        terrain.Id.ShouldBe(MakaMekTerrains.Bridge);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Height_ReturnsConstructorValue(int height)
    {
        var terrain = new BridgeTerrain(height, 0);
        terrain.Height.ShouldBe(height);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void ConstructionFactor_ReturnsConstructorValue(int constructionFactor)
    {
        var terrain = new BridgeTerrain(0, constructionFactor);
        terrain.ConstructionFactor.ShouldBe(constructionFactor);
    }

    [Fact]
    public void InterveningFactor_IsZero()
    {
        var terrain = new BridgeTerrain();
        terrain.InterveningFactor.ShouldBe(0);
    }

    [Fact]
    public void MovementCost_Returns1()
    {
        var terrain = new BridgeTerrain();
        terrain.MovementCost.ShouldBe(1);
    }

    #region Serialization Tests

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 50)]
    [InlineData(3, 100)]
    [InlineData(5, 200)]
    public void ToData_ReturnsCorrectTypeHeightAndConstructionFactor(int height, int constructionFactor)
    {
        var terrain = new BridgeTerrain(height, constructionFactor);

        var data = terrain.ToData();

        data.Type.ShouldBe(MakaMekTerrains.Bridge);
        data.Height.ShouldBe(height);
        data.ConstructionFactor.ShouldBe(constructionFactor);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 50)]
    [InlineData(3, 100)]
    [InlineData(5, 200)]
    public void FromData_WithHeightAndConstructionFactor_ReturnsCorrectBridgeTerrain(int height, int constructionFactor)
    {
        var data = new TerrainData
        {
            Type = MakaMekTerrains.Bridge,
            Height = height,
            ConstructionFactor = constructionFactor
        };

        var terrain = Terrain.FromData(data);

        terrain.ShouldBeOfType<BridgeTerrain>();
        terrain.Id.ShouldBe(MakaMekTerrains.Bridge);
        terrain.Height.ShouldBe(height);
        terrain.MovementCost.ShouldBe(1);
        terrain.InterveningFactor.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 50)]
    [InlineData(3, 100)]
    [InlineData(5, 200)]
    public void Roundtrip_PreservesAllProperties(int height, int constructionFactor)
    {
        var original = new BridgeTerrain(height, constructionFactor);

        var data = original.ToData();
        var restored = Terrain.FromData(data);

        restored.ShouldBeOfType<BridgeTerrain>();
        restored.Id.ShouldBe(original.Id);
        restored.Height.ShouldBe(original.Height);
        restored.Height.ShouldBe(height);
        restored.MovementCost.ShouldBe(original.MovementCost);
        restored.InterveningFactor.ShouldBe(original.InterveningFactor);
    }

    #endregion
}
