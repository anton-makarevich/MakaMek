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
        var sut = new Hex(coords, 3);

        // Assert
        sut.Coordinates.ShouldBe(coords);
        sut.Level.ShouldBe(3);
    }

    [Fact]
    public void Constructor_DefaultLevel_IsZero()
    {
        // Arrange & Act
        var sut = new Hex(new HexCoordinates(0, 0));

        // Assert
        sut.Level.ShouldBe(0);
    }

    [Fact]
    public void AddTerrain_AddsTerrainToHex()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        var heavyWoods = new HeavyWoodsTerrain();

        // Act
        sut.AddTerrain(heavyWoods);

        // Assert
        sut.HasTerrain(MakaMekTerrains.HeavyWoods).ShouldBeTrue();
        sut.GetTerrain(MakaMekTerrains.HeavyWoods).ShouldBe(heavyWoods);
    }

    [Fact]
    public void RemoveTerrain_RemovesTerrainFromHex()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        sut.AddTerrain(new HeavyWoodsTerrain());

        // Act
        sut.RemoveTerrain(MakaMekTerrains.HeavyWoods);

        // Assert
        sut.HasTerrain(MakaMekTerrains.HeavyWoods).ShouldBeFalse();
        sut.GetTerrain(MakaMekTerrains.HeavyWoods).ShouldBeNull();
    }
    
    [Fact]
    public void ReplaceTerrains_ReplacesAllTerrainInHex()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        sut.AddTerrain(new HeavyWoodsTerrain());
        sut.AddTerrain(new LightWoodsTerrain());

        // Act  
        sut.ReplaceTerrains([new ClearTerrain()]);

        // Assert
        sut.HasTerrain(MakaMekTerrains.Clear).ShouldBeTrue();
        sut.HasTerrain(MakaMekTerrains.HeavyWoods).ShouldBeFalse();
        sut.HasTerrain(MakaMekTerrains.LightWoods).ShouldBeFalse();
    }

    [Fact]
    public void GetTerrains_ReturnsAllTerrains()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        var heavyWoods = new HeavyWoodsTerrain();
        sut.AddTerrain(heavyWoods);

        // Act
        var terrains = sut.GetTerrains().ToList();

        // Assert
        terrains.Count.ShouldBe(1);
        terrains.ShouldContain(heavyWoods);
    }

    [Fact]
    public void GetCeiling_ReturnsLevelPlusHighestTerrainHeight()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0), 2);
        sut.AddTerrain(new HeavyWoodsTerrain());

        // Act
        var ceiling = sut.GetCeiling();

        // Assert
        ceiling.ShouldBe(4); // Base level (2) + terrain height (2)
    }

    [Fact]
    public void GetCeiling_WithNoTerrain_ReturnsLevel()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0), 2);

        // Act
        var ceiling = sut.GetCeiling();

        // Assert
        ceiling.ShouldBe(2);
    }

    [Fact]
    public void MovementCost_WithNoTerrain_Returns1()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));

        // Act & Assert
        sut.MovementCost.ShouldBe(1);
    }

    [Fact]
    public void MovementCost_WithSingleTerrain_ReturnsTerrainFactor()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        sut.AddTerrain(new LightWoodsTerrain()); // TerrainFactor = 2

        // Act & Assert
        sut.MovementCost.ShouldBe(2);
    }

    [Fact]
    public void MovementCost_WithMultipleTerrains_ReturnsHighestFactor()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        sut.AddTerrain(new ClearTerrain());      // TerrainFactor = 1
        sut.AddTerrain(new LightWoodsTerrain()); // TerrainFactor = 2
        sut.AddTerrain(new HeavyWoodsTerrain()); // TerrainFactor = 3

        // Act & Assert
        sut.MovementCost.ShouldBe(3);
    }

    [Fact]
    public void MovementCost_AfterRemovingHighestTerrain_ReturnsNextHighestFactor()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        sut.AddTerrain(new LightWoodsTerrain()); // TerrainFactor = 2
        sut.AddTerrain(new HeavyWoodsTerrain()); // TerrainFactor = 3

        // Act
        sut.RemoveTerrain(MakaMekTerrains.HeavyWoods);

        // Assert
        sut.MovementCost.ShouldBe(2);
    }

    [Fact]
    public void MovementCost_AfterRemovingAllTerrain_Returns1()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        sut.AddTerrain(new LightWoodsTerrain());
        sut.AddTerrain(new HeavyWoodsTerrain());

        // Act
        sut.RemoveTerrain((MakaMekTerrains.LightWoods));
        sut.RemoveTerrain(MakaMekTerrains.HeavyWoods);

        // Assert
        sut.MovementCost.ShouldBe(1);
    }

    [Fact]
    public void IsHighlightedChanged_ShouldEmit_WhenIsHighlightedChanges()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        var emittedValues = new List<bool>();
        
        using var subscription = sut.IsHighlightedChanged
            .Subscribe(emittedValues.Add);

        // Act
        sut.IsHighlighted = true;
        sut.IsHighlighted = false;
        sut.IsHighlighted = true;

        // Assert
        sut.IsHighlighted.ShouldBeTrue();
        emittedValues.Count.ShouldBe(3);
        emittedValues[0].ShouldBeTrue();
        emittedValues[1].ShouldBeFalse();
        emittedValues[2].ShouldBeTrue();
    }

    [Fact]
    public void IsHighlightedChanged_ShouldNotEmit_WhenValueIsSame()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        var emittedValues = new List<bool>();
        
        using var subscription = sut.IsHighlightedChanged
            .Subscribe(emittedValues.Add);

        // Act
        sut.IsHighlighted = false; // Default value
        sut.IsHighlighted = false; // Same value
        sut.IsHighlighted = true;  // Different value
        sut.IsHighlighted = true;  // Same value

        // Assert
        emittedValues.Count.ShouldBe(1);
        emittedValues[0].ShouldBeTrue();
    }

    [Fact]
    public void Dispose_ShouldCompleteSubjectAndDisposeIt()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        var completedCalled = false;
        var emittedValues = new List<bool>();
        
        using var subscription = sut.IsHighlightedChanged
            .Subscribe(
                emittedValues.Add,
                () => completedCalled = true);

        // Act
        sut.IsHighlighted = true; // Should emit before disposal
        sut.Dispose();

        // Assert
        emittedValues.Count.ShouldBe(1);
        emittedValues[0].ShouldBeTrue();
        completedCalled.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_ShouldNotEmitAfterDisposal()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        var emittedValues = new List<bool>();
        
        using var subscription = sut.IsHighlightedChanged
            .Subscribe(emittedValues.Add);

        // Act
        sut.Dispose();
        
        // This should not emit anything since the subject is disposed
        sut.IsHighlighted = true;

        // Assert
        emittedValues.Count.ShouldBe(0);
    }

    [Fact]
    public void GetLevelDifference_ReturnsCorrectDifference()
    {
        // Arrange
        var hex1 = new Hex(new HexCoordinates(1, 1), 5);
        var hex2 = new Hex(new HexCoordinates(2, 2), 3);

        // Act
        var difference = hex1.GetLevelDifference(hex2);

        // Assert
        difference.ShouldBe(2); // 5 - 3 = 2
    }

    [Fact]
    public void GetLevelDifference_ReturnsNegativeWhenOtherHexIsHigher()
    {
        // Arrange
        var hex1 = new Hex(new HexCoordinates(1, 1), 2);
        var hex2 = new Hex(new HexCoordinates(2, 2), 4);

        // Act
        var difference = hex1.GetLevelDifference(hex2);

        // Assert
        difference.ShouldBe(-2); // 2 - 4 = -2
    }

    [Fact]
    public void GetLevelDifference_ReturnsZeroWhenLevelsAreEqual()
    {
        // Arrange
        var hex1 = new Hex(new HexCoordinates(1, 1), 3);
        var hex2 = new Hex(new HexCoordinates(2, 2), 3);

        // Act
        var difference = hex1.GetLevelDifference(hex2);

        // Assert
        difference.ShouldBe(0); // 3 - 3 = 0
    }

    [Fact]
    public void GetLevelDifference_IsSymmetric()
    {
        // Arrange
        var hex1 = new Hex(new HexCoordinates(1, 1), 6);
        var hex2 = new Hex(new HexCoordinates(2, 2), 2);

        // Act
        var difference1 = hex1.GetLevelDifference(hex2);
        var difference2 = hex2.GetLevelDifference(hex1);

        // Assert
        difference1.ShouldBe(4);  // 6 - 2 = 4
        difference2.ShouldBe(-4); // 2 - 6 = -4
        difference1.ShouldBe(-difference2);
    }

    [Fact]
    public void GetLevelDifference_WorksWithNegativeLevels()
    {
        // Arrange
        var hex1 = new Hex(new HexCoordinates(1, 1), -1);
        var hex2 = new Hex(new HexCoordinates(2, 2), -3);

        // Act
        var difference = hex1.GetLevelDifference(hex2);

        // Assert
        difference.ShouldBe(2); // -1 - (-3) = 2
    }

    [Fact]
    public void Dispose_ShouldBeIdempotent()
    {
        // Arrange
        var sut = new Hex(new HexCoordinates(0, 0));
        var completedCalled = false;
        
        using var subscription = sut.IsHighlightedChanged
            .Subscribe(_ => { }, () => completedCalled = true);

        // Act
        sut.Dispose();
        sut.Dispose(); // Second call should not cause issues

        // Assert
        completedCalled.ShouldBeTrue();
    }
}
