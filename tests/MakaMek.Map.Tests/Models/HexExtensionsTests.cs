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
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new ClearTerrain());

        // Act
        var depth = sut.GetWaterDepth();

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
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new WaterTerrain(terrainHeight));

        // Act
        var depth = sut.GetWaterDepth();

        // Assert
        depth.ShouldBe(expectedDepth);
    }
    
    [Fact]
    public void GetWaterDepth_ReturnsCorrectDepth_WhenHexHasMultipleTerrains()
    {
        // Arrange - hex with both clear and water terrain
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new ClearTerrain());
        sut.AddTerrain(new WaterTerrain(-2));

        // Act
        var depth = sut.GetWaterDepth();

        // Assert
        depth.ShouldBe(2);
    }

    [Fact]
    public void GetElevationChange_ReturnsZero_WhenLevelsAreSame()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 3 };

        var result = toHex.GetElevationChange(fromHex);

        result.ShouldBe(0);
    }

    [Fact]
    public void GetElevationChange_ReturnsPositive_WhenAscending()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 5 };

        var result = toHex.GetElevationChange(fromHex);

        result.ShouldBe(3);
    }

    [Fact]
    public void GetElevationChange_ReturnsNegative_WhenDescending()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 5 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 2 };

        var result = toHex.GetElevationChange(fromHex);

        result.ShouldBe(-3);
    }

    [Fact]
    public void GetElevationChange_AccountsForWaterDepth()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        fromHex.AddTerrain(new WaterTerrain(-1));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 3 };

        var result = toHex.GetElevationChange(fromHex);

        result.ShouldBe(1);
    }

    [Fact]
    public void GetElevationChange_AccountsForDifferentWaterDepths()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 5 };
        fromHex.AddTerrain(new WaterTerrain(-2));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 5 };
        toHex.AddTerrain(new WaterTerrain(-1));

        var result = toHex.GetElevationChange(fromHex);

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

    [Fact]
    public void GetBridgeHeight_ReturnsNull_WhenNoBridgeTerrain()
    {
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new ClearTerrain());

        var height = sut.GetBridgeHeight();

        height.ShouldBeNull();
    }

    [Fact]
    public void GetBridgeHeight_ReturnsHeight_WhenBridgeExists()
    {
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new BridgeTerrain(3, 0));

        var height = sut.GetBridgeHeight();

        height.ShouldBe(3);
    }

    [Fact]
    public void GetBridgeClearance_ReturnsNull_WhenNoBridgeTerrain()
    {
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new ClearTerrain());

        var clearance = sut.GetBridgeClearance();

        clearance.ShouldBeNull();
    }

    [Fact]
    public void GetBridgeClearance_ReturnsBridgeHeight_WhenOverClearTerrain()
    {
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        sut.AddTerrain(new BridgeTerrain(2, 0));

        var clearance = sut.GetBridgeClearance();

        clearance.ShouldBe(2);
    }

    [Fact]
    public void GetBridgeClearance_ReturnsCorrectClearance_WhenOverWater()
    {
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        sut.AddTerrain(new BridgeTerrain(1, 0));
        sut.AddTerrain(new WaterTerrain(-1));

        var clearance = sut.GetBridgeClearance();

        clearance.ShouldBe(2);
    }

    [Fact]
    public void GetBridgeClearance_ReturnsCorrectClearance_WhenHexLevelIsAboveZero()
    {
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        sut.AddTerrain(new BridgeTerrain(1, 0));
        sut.AddTerrain(new WaterTerrain(-1));

        var clearance = sut.GetBridgeClearance();

        clearance.ShouldBe(2);
    }

    [Fact]
    public void GetBridgeElevationChange_NoBridge_ReturnsNormalElevationChange()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 5 };
        toHex.AddTerrain(new ClearTerrain());

        var result = toHex.GetBridgeElevationChange(fromHex, 2);

        result.ShouldBe(3);
    }

    [Fact]
    public void GetBridgeElevationChange_BothOnBridge_ReturnsSurfaceLevelDifference()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new BridgeTerrain(2, 0));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 1 };
        toHex.AddTerrain(new BridgeTerrain(3, 0));

        var result = toHex.GetBridgeElevationChange(fromHex, 5);

        result.ShouldBe(2); // (1 + 3) - (0 + 2) = 2
    }

    [Fact]
    public void GetBridgeElevationChange_RoadToBridge_UsesBridgeSurfaceLevel()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new RoadTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new BridgeTerrain(1, 0));

        var result = toHex.GetBridgeElevationChange(fromHex, 3);

        result.ShouldBe(1); // (0 + 1) - 0 = 1
    }

    [Fact]
    public void GetBridgeElevationChange_SufficientClearance_ReturnsBottomLevelDifference()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new BridgeTerrain(2, 0));

        var result = toHex.GetBridgeElevationChange(fromHex, 2);

        result.ShouldBe(0); // clearance is 2, unit height is 2, can pass under → bottom level diff
    }

    [Fact]
    public void GetBridgeElevationChange_InsufficientClearance_ReturnsBridgeSurfaceLevelDifference()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new BridgeTerrain(1, 0));

        var result = toHex.GetBridgeElevationChange(fromHex, 2);

        result.ShouldBe(1); // clearance is 1, unit height is 2 → climb to (0 + 1) - 0 = 1
    }

    [Fact]
    public void GetBridgeElevationChange_UnitHeightZero_ReturnsNormalElevationChange()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new BridgeTerrain(1, 0));

        var result = toHex.GetBridgeElevationChange(fromHex, 0);

        result.ShouldBe(0); // no height → can pass under, normal elevation
    }

    [Fact]
    public void GetBridgeElevationChange_OverWaterWithInsufficientClearance_CalculatesFromBottom()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new BridgeTerrain(1, 0));
        toHex.AddTerrain(new WaterTerrain(-1));

        var result = toHex.GetBridgeElevationChange(fromHex, 3);

        // clearance = (0 + 1) - (-1) = 2, unit height 3 > 2 → climb
        // bridge surface (0 + 1) - from bottom 0 = 1
        result.ShouldBe(1);
    }

    [Fact]
    public void GetBridgeElevationChange_OverWaterWithSufficientClearance_UsesBottomLevel()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new BridgeTerrain(1, 0));
        toHex.AddTerrain(new WaterTerrain(-1));

        var result = toHex.GetBridgeElevationChange(fromHex, 2);

        // clearance = (0 + 1) - (-1) = 2, unit height 2 <= 2 → pass under
        // bottom level diff = -1 - 0 = -1 (descend into water)
        result.ShouldBe(-1);
    }

    [Fact]
    public void GetBridgeElevationChange_ElevatedFromHexWithBridgeClimb_CalculatesCorrectly()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 1 };
        toHex.AddTerrain(new BridgeTerrain(2, 0));

        var result = toHex.GetBridgeElevationChange(fromHex, 5);

        // bridge surface = 1 + 2 = 3, from bottom = 3
        // elevation change = 3 - 3 = 0
        result.ShouldBe(0);
    }

    [Fact]
    public void GetBottomLevel_ReturnsWaterDepth_IfPresent()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        sut.AddTerrain(new WaterTerrain(-1));
        
        // Act
        var bottomLevel = sut.GetBottomLevel();
        
        // Assert
        bottomLevel.ShouldBe(-1);
    }

    [Fact]
    public void GetBottomLevel_ReturnsHexLevel_WhenNoWater()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        
        // Act
        var bottomLevel = sut.GetBottomLevel();
        
        // Assert
        bottomLevel.ShouldBe(3);   
    }

    [Fact]
    public void GetStandingLevel_ReturnsBottomLevel_WhenNotOnRoadOrBridge()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new ClearTerrain());

        var result = toHex.GetStandingLevel(fromHex);

        result.ShouldBe(0);
    }

    [Fact]
    public void GetStandingLevel_ReturnsBridgeSurface_WhenOnBridge()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new BridgeTerrain(2, 0));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 1 };
        toHex.AddTerrain(new BridgeTerrain(3, 0));

        var result = toHex.GetStandingLevel(fromHex);

        result.ShouldBe(4); // 1 + 3
    }

    [Fact]
    public void GetStandingLevel_ReturnsHexLevel_WhenOnRoadOnly()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new RoadTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new RoadTerrain());

        var result = toHex.GetStandingLevel(fromHex);

        result.ShouldBe(0); // road has no bridge height, falls back to hex.Level
    }

    [Fact]
    public void GetStandingLevel_ReturnsBridgeSurface_WhenFromBridgeToRoad()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new BridgeTerrain(2, 0));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new RoadTerrain());

        var result = toHex.GetStandingLevel(fromHex);

        result.ShouldBe(0); // road has no bridge, returns hex level
    }
}
