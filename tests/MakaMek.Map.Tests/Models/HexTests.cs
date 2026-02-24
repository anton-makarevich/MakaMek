using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

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
    public void ReplaceTerrains_ReplacesAllTerrainInHex()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        hex.AddTerrain(new HeavyWoodsTerrain());
        hex.AddTerrain(new LightWoodsTerrain());

        // Act  
        hex.ReplaceTerrains([new ClearTerrain()]);

        // Assert
        hex.HasTerrain(MakaMekTerrains.Clear).ShouldBeTrue();
        hex.HasTerrain(MakaMekTerrains.HeavyWoods).ShouldBeFalse();
        hex.HasTerrain(MakaMekTerrains.LightWoods).ShouldBeFalse();
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

    [Fact]
    public void IsHighlightedChanged_ShouldEmit_WhenIsHighlightedChanges()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        var emittedValues = new List<bool>();
        
        using var subscription = hex.IsHighlightedChanged
            .Subscribe(emittedValues.Add);

        // Act
        hex.IsHighlighted = true;
        hex.IsHighlighted = false;
        hex.IsHighlighted = true;

        // Assert
        emittedValues.Count.ShouldBe(3);
        emittedValues[0].ShouldBeTrue();
        emittedValues[1].ShouldBeFalse();
        emittedValues[2].ShouldBeTrue();
    }

    [Fact]
    public void IsHighlightedChanged_ShouldNotEmit_WhenValueIsSame()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        var emittedValues = new List<bool>();
        
        using var subscription = hex.IsHighlightedChanged
            .Subscribe(emittedValues.Add);

        // Act
        hex.IsHighlighted = false; // Default value
        hex.IsHighlighted = false; // Same value
        hex.IsHighlighted = true;  // Different value
        hex.IsHighlighted = true;  // Same value

        // Assert
        emittedValues.Count.ShouldBe(1);
        emittedValues[0].ShouldBeTrue();
    }

    [Fact]
    public void Dispose_ShouldCompleteSubjectAndDisposeIt()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        var completedCalled = false;
        var emittedValues = new List<bool>();
        
        using var subscription = hex.IsHighlightedChanged
            .Subscribe(
                emittedValues.Add,
                () => completedCalled = true);

        // Act
        hex.IsHighlighted = true; // Should emit before disposal
        hex.Dispose();

        // Assert
        emittedValues.Count.ShouldBe(1);
        emittedValues[0].ShouldBeTrue();
        completedCalled.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_ShouldNotEmitAfterDisposal()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        var emittedValues = new List<bool>();
        
        using var subscription = hex.IsHighlightedChanged
            .Subscribe(emittedValues.Add);

        // Act
        hex.Dispose();
        
        // This should not emit anything since the subject is disposed
        hex.IsHighlighted = true;

        // Assert
        emittedValues.Count.ShouldBe(0);
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var hex = new Hex(new HexCoordinates(0, 0));
        var completedCalled = false;
        
        using var subscription = hex.IsHighlightedChanged
            .Subscribe(_ => { }, () => completedCalled = true);

        // Act
        hex.Dispose();
        hex.Dispose(); // Second call should not cause issues

        // Assert
        completedCalled.ShouldBeTrue();
    }
}
