using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class HexExtensionsTests
{
    [Fact]
    public void GetWaterDepth_ReturnsNull_WhenNoWaterTerrain()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new ClearTerrain());

        // Act
        var depth = hex.GetWaterDepth();

        // Assert
        depth.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 0)]   // Shallow water (Height 0) -> Depth 0
    [InlineData(-1, 1)] // Standard depth (Height -1) -> Depth 1
    [InlineData(-2, 2)] // Deep water (Height -2) -> Depth 2
    [InlineData(-3, 3)] // Very deep water (Height -3) -> Depth 3
    public void GetWaterDepth_ReturnsCorrectDepth(int terrainHeight, int expectedDepth)
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new WaterTerrain(terrainHeight));

        // Act
        var depth = hex.GetWaterDepth();

        // Assert
        depth.ShouldBe(expectedDepth);
    }
    
    [Fact]
    public void GetWaterDepth_ReturnsCorrectDepth_WhenHexHasMultipleTerrains()
    {
        // Arrange - hex with both clear and water terrain
        var hex = new Hex(new HexCoordinates(1, 1));
        hex.AddTerrain(new ClearTerrain());
        hex.AddTerrain(new WaterTerrain(-2));

        // Act
        var depth = hex.GetWaterDepth();

        // Assert
        depth.ShouldBe(2);
    }

    [Fact]
    public void GetElevationChangeTo_ReturnsZero_WhenToHexIsNull()
    {
        var hex = new Hex(new HexCoordinates(1, 1)) { Level = 5 };

        var result = hex.GetElevationChangeTo(null);

        result.ShouldBe(0);
    }

    [Fact]
    public void GetElevationChangeTo_ReturnsZero_WhenLevelsAreSame()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 3 };

        var result = fromHex.GetElevationChangeTo(toHex);

        result.ShouldBe(0);
    }

    [Fact]
    public void GetElevationChangeTo_ReturnsPositive_WhenAscending()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 5 };

        var result = fromHex.GetElevationChangeTo(toHex);

        result.ShouldBe(3);
    }

    [Fact]
    public void GetElevationChangeTo_ReturnsNegative_WhenDescending()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 5 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 2 };

        var result = fromHex.GetElevationChangeTo(toHex);

        result.ShouldBe(-3);
    }

    [Fact]
    public void GetElevationChangeTo_AccountsForWaterDepth()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        fromHex.AddTerrain(new WaterTerrain(-1));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 3 };

        var result = fromHex.GetElevationChangeTo(toHex);

        result.ShouldBe(1);
    }

    [Fact]
    public void GetElevationChangeTo_AccountsForDifferentWaterDepths()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 5 };
        fromHex.AddTerrain(new WaterTerrain(-2));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 5 };
        toHex.AddTerrain(new WaterTerrain(-1));

        var result = fromHex.GetElevationChangeTo(toHex);

        result.ShouldBe(1);
    }

    [Theory]
    [InlineData(MakaMekTerrains.Road, true)]
    [InlineData(MakaMekTerrains.Bridge, true)]
    [InlineData(MakaMekTerrains.Pavement, true)]
    [InlineData(MakaMekTerrains.Water, false)]
    [InlineData(MakaMekTerrains.LightWoods, false)]
    [InlineData(MakaMekTerrains.Clear, false)]
    public void HasHardPavement_ReturnsRightValue(MakaMekTerrains terrainType, bool expectedResult)
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(Terrain.CreateTerrainOfType(terrainType));
        
        // Act
        var result = sut.HasHardPavement();
        
        // Assert
        result.ShouldBe(expectedResult);
    }

    [Theory]
    [InlineData(MakaMekTerrains.Bridge, MakaMekTerrains.Bridge)]
    [InlineData(MakaMekTerrains.Pavement, MakaMekTerrains.Pavement)]
    [InlineData(MakaMekTerrains.Road, MakaMekTerrains.Road)]
    [InlineData(MakaMekTerrains.HeavyWoods, null)]
    public void GetRoadOrPavedTerrainId_Returns_PavedTerrain_OrNull(MakaMekTerrains terrainToAdd,
        MakaMekTerrains? expectedTerrain)
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(Terrain.CreateTerrainOfType(terrainToAdd));

        // Act
        var terrain = sut.GetRoadOrPavedTerrainId();
            
        // Assert
        terrain.ShouldBe(expectedTerrain);
    }

    [Theory]
    [InlineData(MakaMekTerrains.Road, MakaMekTerrains.Road, true)]
    [InlineData(MakaMekTerrains.Bridge, MakaMekTerrains.Road, true)]
    [InlineData(MakaMekTerrains.Road, MakaMekTerrains.Bridge, true)]
    [InlineData(MakaMekTerrains.Road, MakaMekTerrains.Pavement, false)]
    [InlineData(MakaMekTerrains.Pavement, MakaMekTerrains.Road, false)]
    [InlineData(MakaMekTerrains.Water, MakaMekTerrains.Bridge, false)]
    [InlineData(MakaMekTerrains.LightWoods, MakaMekTerrains.Road, false)]
    public void IsOnRoadOrBridge_Should_ReturnCorrectValue(MakaMekTerrains fromHexTerrain, MakaMekTerrains toHexTerrain,
        bool isOnRoad)
    {
        // Arrange
        var fromHex = new Hex(new HexCoordinates(1, 1));
        fromHex.AddTerrain(Terrain.CreateTerrainOfType(fromHexTerrain));
        var toHex = new Hex(new HexCoordinates(2, 1));
        toHex.AddTerrain(Terrain.CreateTerrainOfType(toHexTerrain));
        
        // Act
        var result = fromHex.IsOnRoadOrBridge(toHex);
        
        // Assert
        result.ShouldBe(isOnRoad);
    }
}
