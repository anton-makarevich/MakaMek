using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class BridgeTerrainTests
{
    [Fact]
    public void Id_ReturnsBridge()
    {
        var sut = new BridgeTerrain();
        sut.Id.ShouldBe(MakaMekTerrains.Bridge);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Height_ReturnsConstructorValue(int height)
    {
        var sut = new BridgeTerrain(height, 0);
        sut.Height.ShouldBe(height);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public void ConstructionFactor_ReturnsConstructorValue(int constructionFactor)
    {
        var sut = new BridgeTerrain(0, constructionFactor);
        sut.ConstructionFactor.ShouldBe(constructionFactor);
    }

    [Fact]
    public void InterveningFactor_IsZero()
    {
        var sut = new BridgeTerrain();
        sut.InterveningFactor.ShouldBe(0);
    }

    [Fact]
    public void MovementCost_Returns1()
    {
        var sut = new BridgeTerrain();
        sut.MovementCost.ShouldBe(1);
    }

    #region Serialization Tests

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 50)]
    [InlineData(3, 100)]
    [InlineData(5, 200)]
    public void ToData_ReturnsCorrectTypeHeightAndConstructionFactor(int height, int constructionFactor)
    {
        var sut = new BridgeTerrain(height, constructionFactor);

        var data = sut.ToData();

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

        var sut = Terrain.FromData(data);

        sut.ShouldBeOfType<BridgeTerrain>();
        sut.Id.ShouldBe(MakaMekTerrains.Bridge);
        sut.Height.ShouldBe(height);
        sut.MovementCost.ShouldBe(1);
        sut.InterveningFactor.ShouldBe(0);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 50)]
    [InlineData(3, 100)]
    [InlineData(5, 200)]
    public void Roundtrip_PreservesAllProperties(int height, int constructionFactor)
    {
        var sut = new BridgeTerrain(height, constructionFactor);

        var data = sut.ToData();
        var restored = Terrain.FromData(data);

        restored.ShouldBeOfType<BridgeTerrain>();
        restored.Id.ShouldBe(sut.Id);
        restored.Height.ShouldBe(sut.Height);
        restored.Height.ShouldBe(height);
        restored.MovementCost.ShouldBe(sut.MovementCost);
        restored.InterveningFactor.ShouldBe(sut.InterveningFactor);
    }

    #endregion
}
