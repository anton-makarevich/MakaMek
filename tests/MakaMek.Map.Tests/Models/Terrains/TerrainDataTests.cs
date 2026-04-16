using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Terrains;

public class TerrainDataTests
{
    #region ToData Tests

    [Fact]
    public void ToData_ClearTerrain_ReturnsCorrectTypeAndNullHeight()
    {
        // Arrange
        var terrain = new ClearTerrain();

        // Act
        var data = terrain.ToData();

        // Assert
        data.Type.ShouldBe(MakaMekTerrains.Clear);
        data.Height.ShouldBeNull();
    }

    [Fact]
    public void ToData_LightWoodsTerrain_ReturnsCorrectTypeAndNullHeight()
    {
        // Arrange
        var terrain = new LightWoodsTerrain();

        // Act
        var data = terrain.ToData();

        // Assert
        data.Type.ShouldBe(MakaMekTerrains.LightWoods);
        data.Height.ShouldBeNull();
    }

    [Fact]
    public void ToData_HeavyWoodsTerrain_ReturnsCorrectTypeAndNullHeight()
    {
        // Arrange
        var terrain = new HeavyWoodsTerrain();

        // Act
        var data = terrain.ToData();

        // Assert
        data.Type.ShouldBe(MakaMekTerrains.HeavyWoods);
        data.Height.ShouldBeNull();
    }

    [Fact]
    public void ToData_RoughTerrain_ReturnsCorrectTypeAndNullHeight()
    {
        // Arrange
        var terrain = new RoughTerrain();

        // Act
        var data = terrain.ToData();

        // Assert
        data.Type.ShouldBe(MakaMekTerrains.Rough);
        data.Height.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-3)]
    public void ToData_WaterTerrain_ReturnsCorrectTypeAndDepth(int depth)
    {
        // Arrange
        var terrain = new WaterTerrain(depth);

        // Act
        var data = terrain.ToData();

        // Assert
        data.Type.ShouldBe(MakaMekTerrains.Water);
        data.Height.ShouldBe(depth);
    }

    #endregion

    #region FromData Tests

    [Fact]
    public void FromData_ClearTerrain_ReturnsClearTerrainInstance()
    {
        // Arrange
        var data = new TerrainData { Type = MakaMekTerrains.Clear };

        // Act
        var terrain = Terrain.FromData(data);

        // Assert
        terrain.ShouldBeOfType<ClearTerrain>();
        terrain.Id.ShouldBe(MakaMekTerrains.Clear);
        terrain.Height.ShouldBe(0);
    }

    [Fact]
    public void FromData_LightWoodsTerrain_ReturnsLightWoodsTerrainInstance()
    {
        // Arrange
        var data = new TerrainData { Type = MakaMekTerrains.LightWoods };

        // Act
        var terrain = Terrain.FromData(data);

        // Assert
        terrain.ShouldBeOfType<LightWoodsTerrain>();
        terrain.Id.ShouldBe(MakaMekTerrains.LightWoods);
        terrain.Height.ShouldBe(2);
    }

    [Fact]
    public void FromData_HeavyWoodsTerrain_ReturnsHeavyWoodsTerrainInstance()
    {
        // Arrange
        var data = new TerrainData { Type = MakaMekTerrains.HeavyWoods };

        // Act
        var terrain = Terrain.FromData(data);

        // Assert
        terrain.ShouldBeOfType<HeavyWoodsTerrain>();
        terrain.Id.ShouldBe(MakaMekTerrains.HeavyWoods);
        terrain.Height.ShouldBe(2);
    }

    [Fact]
    public void FromData_RoughTerrain_ReturnsRoughTerrainInstance()
    {
        // Arrange
        var data = new TerrainData { Type = MakaMekTerrains.Rough };

        // Act
        var terrain = Terrain.FromData(data);

        // Assert
        terrain.ShouldBeOfType<RoughTerrain>();
        terrain.Id.ShouldBe(MakaMekTerrains.Rough);
        terrain.Height.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-3)]
    public void FromData_WaterTerrain_WithHeight_ReturnsWaterTerrainWithCorrectDepth(int depth)
    {
        // Arrange
        var data = new TerrainData { Type = MakaMekTerrains.Water, Height = depth };

        // Act
        var terrain = Terrain.FromData(data);

        // Assert
        terrain.ShouldBeOfType<WaterTerrain>();
        terrain.Id.ShouldBe(MakaMekTerrains.Water);
        terrain.Height.ShouldBe(depth);
    }

    [Fact]
    public void FromData_WaterTerrain_WithNullHeight_ReturnsShallowWater()
    {
        // Arrange
        var data = new TerrainData { Type = MakaMekTerrains.Water, Height = null };

        // Act
        var terrain = Terrain.FromData(data);

        // Assert
        terrain.ShouldBeOfType<WaterTerrain>();
        terrain.Id.ShouldBe(MakaMekTerrains.Water);
        terrain.Height.ShouldBe(0); // Default to shallow when height is null
    }

    [Fact]
    public void FromData_UnknownTerrain_ThrowsArgumentException()
    {
        // Arrange
        var invalidTerrainType = (MakaMekTerrains)999;
        var data = new TerrainData { Type = invalidTerrainType };

        // Act & Assert
        Should.Throw<ArgumentException>(() => Terrain.FromData(data))
            .Message.ShouldContain("Unknown terrain type");
    }

    #endregion

    #region Roundtrip Tests

    [Theory]
    [InlineData(MakaMekTerrains.Clear)]
    [InlineData(MakaMekTerrains.LightWoods)]
    [InlineData(MakaMekTerrains.HeavyWoods)]
    [InlineData(MakaMekTerrains.Rough)]
    public void Roundtrip_NonWaterTerrains_PreservesAllProperties(MakaMekTerrains terrainType)
    {
        // Arrange
        var original = Terrain.GetTerrainType(terrainType);

        // Act
        var data = original.ToData();
        var restored = Terrain.FromData(data);

        // Assert
        restored.Id.ShouldBe(original.Id);
        restored.Height.ShouldBe(original.Height);
        restored.MovementCost.ShouldBe(original.MovementCost);
        restored.InterveningFactor.ShouldBe(original.InterveningFactor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(-3)]
    public void Roundtrip_WaterTerrain_PreservesDepthAndAllProperties(int depth)
    {
        // Arrange
        var original = new WaterTerrain(depth);

        // Act
        var data = original.ToData();
        var restored = Terrain.FromData(data);

        // Assert
        restored.ShouldBeOfType<WaterTerrain>();
        restored.Id.ShouldBe(original.Id);
        restored.Height.ShouldBe(original.Height);
        restored.Height.ShouldBe(depth);
        restored.MovementCost.ShouldBe(original.MovementCost);
        restored.InterveningFactor.ShouldBe(original.InterveningFactor);
    }

    #endregion
}
