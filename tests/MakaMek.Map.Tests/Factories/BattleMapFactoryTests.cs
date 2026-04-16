using NSubstitute;
using Sanet.MakaMek.Map.Data;
using Sanet.MakaMek.Map.Factories;
using Sanet.MakaMek.Map.Generators;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Factories;

public class BattleMapFactoryTests
{
    private readonly BattleMapFactory _sut = new();

    [Fact]
    public void GenerateMap_CreatesCorrectSizedMap()
    {
        // Arrange
        const int width = 5;
        const int height = 4;
        var generator = Substitute.For<ITerrainGenerator>();
        generator.Generate(Arg.Any<HexCoordinates>())
            .Returns(c => new Hex(c.Arg<HexCoordinates>()));

        // Act
        var map = _sut.GenerateMap(width, height, generator);

        // Assert
        map.Width.ShouldBe(width);
        map.Height.ShouldBe(height);

        // Check if all hexes are created
        for (var r = 1; r < height+1; r++)
        {
            for (var q = 1; q < width+1; q++)
            {
                var hex = map.GetHex(new HexCoordinates(q, r));
                hex.ShouldNotBeNull();
            }
        }

        // Verify generator was called for each hex
        generator.Received(width * height).Generate(Arg.Any<HexCoordinates>());
    }

    [Fact]
    public void GenerateMap_WithTerrainGenerator_CreatesCorrectTerrain()
    {
        // Arrange
        const int width = 3;
        const int height = 3;
        var generator = Substitute.For<ITerrainGenerator>();
        generator.Generate(Arg.Any<HexCoordinates>())
            .Returns(c => {
                var hex = new Hex(c.Arg<HexCoordinates>());
                hex.AddTerrain(new ClearTerrain());
                return hex;
            });

        // Act
        var map = _sut.GenerateMap(width, height, generator);

        // Assert
        // Check all hexes have clear terrain
        for (var q = 1; q < width+1; q++)
        {
            for (var r = 1; r < height+1; r++)
            {
                var hex = map.GetHex(new HexCoordinates(q, r));
                hex!.HasTerrain(MakaMekTerrains.Clear).ShouldBeTrue();
            }
        }

        // Verify generator was called for each hex
        generator.Received(width * height).Generate(Arg.Any<HexCoordinates>());
    }
    
    [Fact]
    public void CreateFromData_ShouldCloneHexesCorrectly()
    {
        // Arrange
        var originalMap = new BattleMap(3, 3);
        // Add hexes with different terrains and levels
        var hex1 = new Hex(new HexCoordinates(1, 1));
        hex1.AddTerrain(new ClearTerrain());
        originalMap.AddHex(hex1);
        
        var hex2 = new Hex(new HexCoordinates(2, 2), 1);
        hex2.AddTerrain(new LightWoodsTerrain());
        originalMap.AddHex(hex2);
        
        var hex3 = new Hex(new HexCoordinates(3, 3), 2);
        hex3.AddTerrain(new HeavyWoodsTerrain());
        originalMap.AddHex(hex3);
        
        var hexDataList = originalMap.ToData();

        // Act
        var clonedMap = _sut.CreateFromData(hexDataList);

        // Assert
        foreach (var hex in originalMap.GetHexes())
        {
            var clonedHex = clonedMap.GetHex(hex.Coordinates);
            clonedHex.ShouldNotBeNull();
            clonedHex.Level.ShouldBe(hex.Level);
            clonedHex.GetTerrainTypes().ShouldBeEquivalentTo(hex.GetTerrainTypes());
            
            // Verify terrain properties are preserved
            foreach (var terrain in hex.GetTerrains())
            {
                var clonedTerrain = clonedHex.GetTerrain(terrain.Id);
                clonedTerrain.ShouldNotBeNull();
                clonedTerrain.Height.ShouldBe(terrain.Height);
                clonedTerrain.MovementCost.ShouldBe(terrain.MovementCost);
            }
        }
    }

    [Fact]
    public void CreateFromData_ShouldPreserveWaterDepth()
    {
        // Arrange
        var originalMap = new BattleMap(3, 3);
        
        // Add hexes with water at different depths
        var shallowWaterHex = new Hex(new HexCoordinates(1, 1));
        shallowWaterHex.AddTerrain(new WaterTerrain(0)); // Shallow water
        originalMap.AddHex(shallowWaterHex);
        
        var standardWaterHex = new Hex(new HexCoordinates(2, 2));
        standardWaterHex.AddTerrain(new WaterTerrain(-1)); // Standard depth
        originalMap.AddHex(standardWaterHex);
        
        var deepWaterHex = new Hex(new HexCoordinates(3, 3));
        deepWaterHex.AddTerrain(new WaterTerrain(-2)); // Deep water
        originalMap.AddHex(deepWaterHex);
        
        var mapData = originalMap.ToData();

        // Act
        var clonedMap = _sut.CreateFromData(mapData);

        // Assert
        // Shallow water (depth 0)
        var clonedShallow = clonedMap.GetHex(new HexCoordinates(1, 1));
        clonedShallow.ShouldNotBeNull();
        var shallowWater = clonedShallow.GetTerrain(MakaMekTerrains.Water) as WaterTerrain;
        shallowWater.ShouldNotBeNull();
        shallowWater.Height.ShouldBe(0);
        shallowWater.MovementCost.ShouldBe(1); // Shallow water = 1 MP

        // Standard water (depth -1)
        var clonedStandard = clonedMap.GetHex(new HexCoordinates(2, 2));
        clonedStandard.ShouldNotBeNull();
        var standardWater = clonedStandard.GetTerrain(MakaMekTerrains.Water) as WaterTerrain;
        standardWater.ShouldNotBeNull();
        standardWater.Height.ShouldBe(-1);
        standardWater.MovementCost.ShouldBe(2); // Standard depth = 2 MP

        // Deep water (depth -2)
        var clonedDeep = clonedMap.GetHex(new HexCoordinates(3, 3));
        clonedDeep.ShouldNotBeNull();
        var deepWater = clonedDeep.GetTerrain(MakaMekTerrains.Water) as WaterTerrain;
        deepWater.ShouldNotBeNull();
        deepWater.Height.ShouldBe(-2);
        deepWater.MovementCost.ShouldBe(4); // Deep water = 4 MP
    }

    [Fact]
    public void CreateFromData_ShouldReturnEmptyMap_WhenHexDataIsEmpty()
    {
        // Arrange
        var emptyMapData = new BattleMapData
        {
            HexData = [],
            Biome = "TestBiome"
        };

        // Act
        var result = _sut.CreateFromData(emptyMapData);

        // Assert
        result.Width.ShouldBe(0);
        result.Height.ShouldBe(0);
        result.Biome.ShouldBe("TestBiome");
        result.GetHexes().ShouldBeEmpty();
    }
}