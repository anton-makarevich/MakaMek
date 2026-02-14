using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Map;

public class HexTests
{
    [Fact]
    public void Constructor_SetsCoordinatesAndLevel()
    {
        // Arrange
        var coords = new HexCoordinates(1, 2);

        // Act
        var hex = new Hex(coords, 3);

        // Assert
        hex.Coordinates.ShouldBe(coords);
        hex.Level.ShouldBe(3);
    }

    [Fact]
    public void Constructor_DefaultLevel_IsZero()
    {
        // Arrange & Act
        var hex = new Hex(new HexCoordinates(0, 0));

        // Assert
        hex.Level.ShouldBe(0);
    }

    [Fact]
    public void AddTerrain_AddsTerrainToHex()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        var heavyWoods = new HeavyWoodsTerrain();

        // Act
        hex.AddTerrain(heavyWoods);

        // Assert
        hex.HasTerrain(MakaMekTerrains.HeavyWoods).ShouldBeTrue();
        hex.GetTerrain(MakaMekTerrains.HeavyWoods).ShouldBe(heavyWoods);
    }

    [Fact]
    public void RemoveTerrain_RemovesTerrainFromHex()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        hex.AddTerrain(new HeavyWoodsTerrain());

        // Act
        hex.RemoveTerrain(MakaMekTerrains.HeavyWoods);

        // Assert
        hex.HasTerrain(MakaMekTerrains.HeavyWoods).ShouldBeFalse();
        hex.GetTerrain(MakaMekTerrains.HeavyWoods).ShouldBeNull();
    }

    [Fact]
    public void GetTerrains_ReturnsAllTerrains()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        var heavyWoods = new HeavyWoodsTerrain();
        hex.AddTerrain(heavyWoods);

        // Act
        var terrains = hex.GetTerrains().ToList();

        // Assert
        terrains.Count.ShouldBe(1);
        terrains.ShouldContain(heavyWoods);
    }

    [Fact]
    public void GetCeiling_ReturnsLevelPlusHighestTerrainHeight()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0), 2);
        hex.AddTerrain(new HeavyWoodsTerrain());

        // Act
        var ceiling = hex.GetCeiling();

        // Assert
        ceiling.ShouldBe(4); // Base level (2) + terrain height (2)
    }

    [Fact]
    public void GetCeiling_WithNoTerrain_ReturnsLevel()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0), 2);

        // Act
        var ceiling = hex.GetCeiling();

        // Assert
        ceiling.ShouldBe(2);
    }

    [Fact]
    public void MovementCost_WithNoTerrain_Returns1()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));

        // Act & Assert
        hex.MovementCost.ShouldBe(1);
    }

    [Fact]
    public void MovementCost_WithSingleTerrain_ReturnsTerrainFactor()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        hex.AddTerrain(new LightWoodsTerrain()); // TerrainFactor = 2

        // Act & Assert
        hex.MovementCost.ShouldBe(2);
    }

    [Fact]
    public void MovementCost_WithMultipleTerrains_ReturnsHighestFactor()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        hex.AddTerrain(new ClearTerrain());      // TerrainFactor = 1
        hex.AddTerrain(new LightWoodsTerrain()); // TerrainFactor = 2
        hex.AddTerrain(new HeavyWoodsTerrain()); // TerrainFactor = 3

        // Act & Assert
        hex.MovementCost.ShouldBe(3);
    }

    [Fact]
    public void MovementCost_AfterRemovingHighestTerrain_ReturnsNextHighestFactor()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        hex.AddTerrain(new LightWoodsTerrain()); // TerrainFactor = 2
        hex.AddTerrain(new HeavyWoodsTerrain()); // TerrainFactor = 3

        // Act
        hex.RemoveTerrain(MakaMekTerrains.HeavyWoods);

        // Assert
        hex.MovementCost.ShouldBe(2);
    }

    [Fact]
    public void MovementCost_AfterRemovingAllTerrain_Returns1()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        hex.AddTerrain(new LightWoodsTerrain());
        hex.AddTerrain(new HeavyWoodsTerrain());

        // Act
        hex.RemoveTerrain((MakaMekTerrains.LightWoods));
        hex.RemoveTerrain(MakaMekTerrains.HeavyWoods);

        // Assert
        hex.MovementCost.ShouldBe(1);
    }
}
