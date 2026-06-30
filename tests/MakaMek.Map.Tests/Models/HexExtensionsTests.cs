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
    public void GetGroundElevationChange_ReturnsZero_WhenLevelsAreSame()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 3 };

        var result = toHex.GetGroundElevationChange(fromHex);

        result.ShouldBe(0);
    }

    [Fact]
    public void GetGroundElevationChange_ReturnsPositive_WhenAscending()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 5 };

        var result = toHex.GetGroundElevationChange(fromHex);

        result.ShouldBe(3);
    }

    [Fact]
    public void GetGroundElevationChange_ReturnsNegative_WhenDescending()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 5 };
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 2 };

        var result = toHex.GetGroundElevationChange(fromHex);

        result.ShouldBe(-3);
    }

    [Fact]
    public void GetGroundElevationChange_AccountsForWaterDepth()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        fromHex.AddTerrain(new WaterTerrain(-1));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 3 };

        var result = toHex.GetGroundElevationChange(fromHex);

        result.ShouldBe(1);
    }

    [Fact]
    public void GetGroundElevationChange_AccountsForDifferentWaterDepths()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 5 };
        fromHex.AddTerrain(new WaterTerrain(-2));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 5 };
        toHex.AddTerrain(new WaterTerrain(-1));

        var result = toHex.GetGroundElevationChange(fromHex);

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
    public void GetRoadOrPavedTerrain_Returns_PavedTerrain_OrNull(MakaMekTerrains terrainToAdd,
        MakaMekTerrains? expectedTerrain)
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(Terrain.CreateTerrainOfType(terrainToAdd));

        // Act
        var terrainId = sut.GetRoadOrPavedTerrain()?.Id;
            
        // Assert
        terrainId.ShouldBe(expectedTerrain);
    }

    [Theory]
    [InlineData(MakaMekTerrains.Road, MakaMekTerrains.Road, HexSurface.Ground, HexSurface.Ground, true)]
    [InlineData(MakaMekTerrains.Bridge, MakaMekTerrains.Road, HexSurface.Bridge, HexSurface.Ground, true)]
    [InlineData(MakaMekTerrains.Road, MakaMekTerrains.Bridge, HexSurface.Ground, HexSurface.Bridge, true)]
    [InlineData(MakaMekTerrains.Road, MakaMekTerrains.Pavement, HexSurface.Ground, HexSurface.Ground, false)]
    [InlineData(MakaMekTerrains.Pavement, MakaMekTerrains.Road, HexSurface.Ground, HexSurface.Ground, false)]
    [InlineData(MakaMekTerrains.Water, MakaMekTerrains.Bridge, HexSurface.Bridge, HexSurface.Ground, false)]
    [InlineData(MakaMekTerrains.LightWoods, MakaMekTerrains.Road, HexSurface.Ground, HexSurface.Ground, false)]
    public void IsOnRoadOrBridge_Should_ReturnCorrectValue(MakaMekTerrains fromHexTerrain,
        MakaMekTerrains toHexTerrain, HexSurface toSurface, HexSurface fromSurface, bool isOnRoad)
    {
        // Arrange
        var fromHex = new Hex(new HexCoordinates(1, 1));
        fromHex.AddTerrain(Terrain.CreateTerrainOfType(fromHexTerrain));
        var toHex = new Hex(new HexCoordinates(2, 1));
        toHex.AddTerrain(Terrain.CreateTerrainOfType(toHexTerrain));
        
        // Act
        var result = fromHex.IsOnRoadOrBridge(toHex, fromSurface, toSurface);
        
        // Assert
        result.ShouldBe(isOnRoad);
    }

    [Fact]
    public void IsOnRoadOrBridge_GroundToGroundThroughBridgeHex_ReturnsFalse()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1));
        fromHex.AddTerrain(new BridgeTerrain(1, 0));
        var toHex = new Hex(new HexCoordinates(2, 1));
        toHex.AddTerrain(new BridgeTerrain(1, 0));

        var result = fromHex.IsOnRoadOrBridge(toHex, HexSurface.Ground, HexSurface.Ground);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsOnRoadOrBridge_BridgeToBridge_ReturnsTrue()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1));
        fromHex.AddTerrain(new BridgeTerrain(1, 0));
        var toHex = new Hex(new HexCoordinates(2, 1));
        toHex.AddTerrain(new BridgeTerrain(1, 0));

        var result = fromHex.IsOnRoadOrBridge(toHex, HexSurface.Bridge, HexSurface.Bridge);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsOnRoadOrBridge_RoadToRoadOnGround_ReturnsTrue()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1));
        fromHex.AddTerrain(new RoadTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1));
        toHex.AddTerrain(new RoadTerrain());

        var result = fromHex.IsOnRoadOrBridge(toHex, HexSurface.Ground, HexSurface.Ground);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsOnRoadOrBridge_GroundUnitOnBridgeHexToBridge_ReturnsFalse()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1));
        fromHex.AddTerrain(new BridgeTerrain(1, 0));
        var toHex = new Hex(new HexCoordinates(2, 1));
        toHex.AddTerrain(new BridgeTerrain(1, 0));

        var result = fromHex.IsOnRoadOrBridge(toHex, HexSurface.Ground, HexSurface.Bridge);

        result.ShouldBeFalse();
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
    public void GetElevationChange_NoBridge_ReturnsNormalElevationChange()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 5 };
        toHex.AddTerrain(new ClearTerrain());

        var result = toHex.GetElevationChange(fromHex, HexSurface.Ground, HexSurface.Ground);

        result.ShouldBe(3);
    }

    [Fact]
    public void GetElevationChange_BothOnBridge_ReturnsSurfaceLevelDifference()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new BridgeTerrain(2, 0));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 1 };
        toHex.AddTerrain(new BridgeTerrain(3, 0));

        var result = toHex.GetElevationChange(fromHex, HexSurface.Bridge, HexSurface.Bridge);

        result.ShouldBe(2); // (1 + 3) - (0 + 2) = 2
    }

    [Fact]
    public void GetElevationChange_GroundToBridge_UsesBridgeSurfaceLevel()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new RoadTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new BridgeTerrain(1, 0));

        var result = toHex.GetElevationChange(fromHex, HexSurface.Ground, HexSurface.Bridge);

        result.ShouldBe(1); // (0 + 1) - 0 = 1
    }

    [Fact]
    public void GetElevationChange_GroundToGroundUnderBridge_ReturnsBottomLevelDifference()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new BridgeTerrain(2, 0));

        var result = toHex.GetElevationChange(fromHex, HexSurface.Ground, HexSurface.Ground);

        result.ShouldBe(0);
    }

    [Fact]
    public void GetElevationChange_OverWaterGroundToBridge_UsesBridgeSurface()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new BridgeTerrain(1, 0));
        toHex.AddTerrain(new WaterTerrain(-1));

        var result = toHex.GetElevationChange(fromHex, HexSurface.Ground, HexSurface.Bridge);

        // bridge surface (0 + 1) - from bottom 0 = 1
        result.ShouldBe(1);
    }

    [Fact]
    public void GetElevationChange_OverWaterGroundToGround_UsesBottomLevel()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new BridgeTerrain(1, 0));
        toHex.AddTerrain(new WaterTerrain(-1));

        var result = toHex.GetElevationChange(fromHex, HexSurface.Ground, HexSurface.Ground);

        // bottom level diff = -1 - 0 = -1 (descend into water)
        result.ShouldBe(-1);
    }

    [Fact]
    public void GetElevationChange_BridgeOverWaterToClear_UsesBridgeSurface()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new WaterTerrain(-1));
        fromHex.AddTerrain(new BridgeTerrain(0, 0));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new ClearTerrain());

        var result = toHex.GetElevationChange(fromHex, HexSurface.Bridge, HexSurface.Ground);

        // unit is on bridge surface = 0 + 0 = 0, target bottom = 0
        result.ShouldBe(0);
    }

    [Fact]
    public void GetElevationChange_BridgeOverWaterToRoad_UsesBridgeSurface()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new WaterTerrain(-1));
        fromHex.AddTerrain(new BridgeTerrain(0, 0));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        toHex.AddTerrain(new RoadTerrain());

        var result = toHex.GetElevationChange(fromHex, HexSurface.Bridge, HexSurface.Ground);

        // unit is on bridge surface = 0 + 0 = 0, target bottom = 0
        result.ShouldBe(0);
    }

    [Fact]
    public void GetElevationChange_ElevatedBridgeOverWaterToClear_UsesBridgeSurface()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        fromHex.AddTerrain(new WaterTerrain(-1));
        fromHex.AddTerrain(new BridgeTerrain(2, 0));
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 2 };
        toHex.AddTerrain(new ClearTerrain());

        var result = toHex.GetElevationChange(fromHex, HexSurface.Bridge, HexSurface.Ground);

        // unit is on bridge surface = 0 + 2 = 2, target bottom = 2
        result.ShouldBe(0);
    }

    [Fact]
    public void GetElevationChange_ElevatedFromHexClimbToBridge_CalculatesCorrectly()
    {
        var fromHex = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        fromHex.AddTerrain(new ClearTerrain());
        var toHex = new Hex(new HexCoordinates(2, 1)) { Level = 1 };
        toHex.AddTerrain(new BridgeTerrain(2, 0));

        var result = toHex.GetElevationChange(fromHex, HexSurface.Ground, HexSurface.Bridge);

        // bridge surface = 1 + 2 = 3, from bottom = 3
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
    public void GetStandingLevel_Ground_ReturnsBottomLevel_WhenNoBridge()
    {
        var sut = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        sut.AddTerrain(new ClearTerrain());

        var result = sut.GetStandingLevel(HexSurface.Ground);

        result.ShouldBe(0);
    }

    [Fact]
    public void GetStandingLevel_Ground_ReturnsBottomLevel_WhenWaterPresent()
    {
        var sut = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        sut.AddTerrain(new WaterTerrain(-1));

        var result = sut.GetStandingLevel(HexSurface.Ground);

        result.ShouldBe(-1);
    }

    [Fact]
    public void GetStandingLevel_Bridge_ReturnsHexLevelPlusBridgeHeight()
    {
        var sut = new Hex(new HexCoordinates(2, 1)) { Level = 1 };
        sut.AddTerrain(new BridgeTerrain(3, 0));

        var result = sut.GetStandingLevel(HexSurface.Bridge);

        result.ShouldBe(4);
    }

    [Fact]
    public void GetStandingLevel_Bridge_ReturnsHexLevel_WhenNoBridge()
    {
        var sut = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        sut.AddTerrain(new RoadTerrain());

        var result = sut.GetStandingLevel(HexSurface.Bridge);

        result.ShouldBe(0);
    }

    [Fact]
    public void GetHexSurfaces_ReturnsOnlyGround_WhenNoBridge()
    {
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new ClearTerrain());

        var surfaces = sut.GetHexSurfaces();

        surfaces.ShouldBe([HexSurface.Ground]);
    }

    [Fact]
    public void GetHexSurfaces_ReturnsGroundAndBridge_WhenBridgeExists()
    {
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new BridgeTerrain(2, 0));

        var surfaces = sut.GetHexSurfaces();

        surfaces.ShouldBe([HexSurface.Ground, HexSurface.Bridge]);
    }

    [Fact]
    public void GetHexSurfaces_ReturnsGroundAndBridge_WhenBridgeHeightIsZero()
    {
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new BridgeTerrain(0, 0));

        var surfaces = sut.GetHexSurfaces();

        surfaces.ShouldBe([HexSurface.Ground, HexSurface.Bridge]);
    }

    [Fact]
    public void GetHighestSurface_ReturnsGround_WhenNoBridge()
    {
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new ClearTerrain());

        var result = sut.GetHighestSurface();

        result.ShouldBe(HexSurface.Ground);
    }

    [Fact]
    public void GetHighestSurface_ReturnsBridge_WhenBridgeIsHigher()
    {
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        sut.AddTerrain(new BridgeTerrain(2, 0));

        var result = sut.GetHighestSurface();

        result.ShouldBe(HexSurface.Bridge);
    }

    [Fact]
    public void GetHighestSurface_ReturnsBridge_WhenLevelsAreEqual()
    {
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        sut.AddTerrain(new BridgeTerrain(0, 0));

        var result = sut.GetHighestSurface();

        result.ShouldBe(HexSurface.Bridge);
    }

    [Fact]
    public void GetHighestSurface_ReturnsGround_WhenBridgeHasNegativeHeight()
    {
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        sut.AddTerrain(new BridgeTerrain(-1, 0));

        var result = sut.GetHighestSurface();

        result.ShouldBe(HexSurface.Ground);
    }

    [Fact]
    public void GetRoadSurfaceLevel_NoRoadTerrain_ReturnsNull()
    {
        var sut = new Hex(new HexCoordinates(1, 1));
        sut.AddTerrain(new ClearTerrain());

        var result = sut.GetRoadSurfaceLevel();

        result.ShouldBeNull();
    }

    [Fact]
    public void GetRoadSurfaceLevel_GroundRoad_ReturnsBottomLevel()
    {
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        sut.AddTerrain(new RoadTerrain());

        var result = sut.GetRoadSurfaceLevel();

        result.ShouldBe(3);
    }

    [Fact]
    public void GetRoadSurfaceLevel_Pavement_ReturnsBottomLevel()
    {
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        sut.AddTerrain(new PavementTerrain());

        var result = sut.GetRoadSurfaceLevel();

        result.ShouldBe(2);
    }

    [Fact]
    public void GetRoadSurfaceLevel_Bridge_ReturnsLevelPlusBridgeHeight()
    {
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 1 };
        sut.AddTerrain(new BridgeTerrain(2, 0));

        var result = sut.GetRoadSurfaceLevel();

        result.ShouldBe(3);
    }

    [Fact]
    public void GetRoadSurfaceLevel_BridgeOverWater_ReturnsBridgeSurfaceLevel()
    {
        var sut = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        sut.AddTerrain(new BridgeTerrain(1, 0));
        sut.AddTerrain(new WaterTerrain(-1));

        var result = sut.GetRoadSurfaceLevel();

        result.ShouldBe(1);
    }

    [Fact]
    public void CanRoadConnectTo_NoRoadOnEither_ReturnsFalse()
    {
        var current = new Hex(new HexCoordinates(1, 1));
        current.AddTerrain(new ClearTerrain());
        var neighbor = new Hex(new HexCoordinates(2, 1));
        neighbor.AddTerrain(new ClearTerrain());

        var result = current.CanRoadConnectTo(neighbor);

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanRoadConnectTo_NoRoadOnNeighbor_ReturnsFalse()
    {
        var current = new Hex(new HexCoordinates(1, 1)) { Level = 0 };
        current.AddTerrain(new RoadTerrain());
        var neighbor = new Hex(new HexCoordinates(2, 1));
        neighbor.AddTerrain(new ClearTerrain());

        var result = current.CanRoadConnectTo(neighbor);

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanRoadConnectTo_NoRoadOnCurrent_ReturnsFalse()
    {
        var current = new Hex(new HexCoordinates(1, 1));
        current.AddTerrain(new ClearTerrain());
        var neighbor = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        neighbor.AddTerrain(new RoadTerrain());

        var result = current.CanRoadConnectTo(neighbor);

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanRoadConnectTo_EqualLevelGroundRoads_ReturnsTrue()
    {
        var current = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        current.AddTerrain(new RoadTerrain());
        var neighbor = new Hex(new HexCoordinates(2, 1)) { Level = 2 };
        neighbor.AddTerrain(new RoadTerrain());

        var result = current.CanRoadConnectTo(neighbor);

        result.ShouldBeTrue();
    }

    [Fact]
    public void CanRoadConnectTo_DifferenceOfOne_ReturnsTrue()
    {
        var current = new Hex(new HexCoordinates(1, 1)) { Level = 3 };
        current.AddTerrain(new RoadTerrain());
        var neighbor = new Hex(new HexCoordinates(2, 1)) { Level = 2 };
        neighbor.AddTerrain(new RoadTerrain());

        var result = current.CanRoadConnectTo(neighbor);

        result.ShouldBeTrue();
    }

    [Fact]
    public void CanRoadConnectTo_DifferenceOfTwo_ReturnsFalse()
    {
        var current = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        current.AddTerrain(new RoadTerrain());
        var neighbor = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        neighbor.AddTerrain(new RoadTerrain());

        var result = current.CanRoadConnectTo(neighbor);

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanRoadConnectTo_RoadToBridgeWithHeightOne_ReturnsTrue()
    {
        var current = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        current.AddTerrain(new RoadTerrain());
        var neighbor = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        neighbor.AddTerrain(new BridgeTerrain(1, 0));

        var result = current.CanRoadConnectTo(neighbor);

        result.ShouldBeTrue();
    }

    [Fact]
    public void CanRoadConnectTo_RoadToBridgeWithHeightZero_ReturnsFalseWhenDifferenceTooLarge()
    {
        var current = new Hex(new HexCoordinates(1, 1)) { Level = 2 };
        current.AddTerrain(new RoadTerrain());
        var neighbor = new Hex(new HexCoordinates(2, 1)) { Level = 0 };
        neighbor.AddTerrain(new BridgeTerrain(0, 0));

        var result = current.CanRoadConnectTo(neighbor);

        result.ShouldBeFalse();
    }

    [Fact]
    public void CanRoadConnectTo_PavementToPavementEqualLevel_ReturnsTrue()
    {
        var current = new Hex(new HexCoordinates(1, 1)) { Level = 1 };
        current.AddTerrain(new PavementTerrain());
        var neighbor = new Hex(new HexCoordinates(2, 1)) { Level = 1 };
        neighbor.AddTerrain(new PavementTerrain());

        var result = current.CanRoadConnectTo(neighbor);

        result.ShouldBeTrue();
    }
}
