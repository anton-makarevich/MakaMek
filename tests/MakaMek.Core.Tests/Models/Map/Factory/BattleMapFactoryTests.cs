using NSubstitute;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Core.Models.Map.Factory;
using Sanet.MakaMek.Core.Models.Map.Terrains;
using Sanet.MakaMek.Core.Utils.Generators;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Map.Factory;

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
        var hex1 = new Hex(new HexCoordinates(1, 1), 0);
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
        }
    }
}