using NSubstitute;
using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Terrains;
using Sanet.MakaMek.Map.Services;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Services;

public class TerrainBitmaskServiceTests
{
    private readonly TerrainBitmaskService _sut = new();

    private static void SetupNeighborsByBitmask(
        IBattleMap map,
        HexCoordinates centerCoords,
        IEnumerable<HexDirection> directions,
        MakaMekTerrains terrain)
    {
        foreach (var direction in directions)
        {
            var neighborCoords = centerCoords.GetNeighbour(direction);
            var neighborHex = CreateHexWithTerrain(terrain);
            map.GetHex(neighborCoords).Returns(neighborHex);
        }
    }

    [Fact]
    public void ComputeRawBitmask_NoNeighborsWithTerrain_ReturnsZero()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        SetupNeighborsByBitmask(map, centerCoords, directions, MakaMekTerrains.Clear);

        // Act
        var result = _sut.ComputeRawBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.ShouldBe((byte)0);
    }

    [Fact]
    public void ComputeRawBitmask_AllNeighborsWithTerrain_ReturnsAllBitsSet()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        SetupNeighborsByBitmask(map, centerCoords, directions, MakaMekTerrains.Water);

        // Act
        var result = _sut.ComputeRawBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.ShouldBe((byte)0b111111); // All 6 bits set
    }

    [Fact]
    public void ComputeRawBitmask_SingleNeighborInTopDirection_SetsBit0()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        foreach (var direction in directions)
        {
            var terrainType = direction == HexDirection.Top ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeRawBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.ShouldBe((byte)0b000001); // Only bit 0 set
    }

    [Fact]
    public void ComputeRawBitmask_SingleNeighborInTopLeftDirection_SetsBit5()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        foreach (var direction in directions)
        {
            var terrainType = direction == HexDirection.TopLeft ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeRawBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.ShouldBe((byte)0b100000); // Only bit 5 set
    }

    [Fact]
    public void ComputeRawBitmask_MultipleNeighborsWithTerrain_SetsCorrectBits()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        // Top (0), BottomRight (2), and BottomLeft (4) have water
        const byte expectedMask = 1 << 0 | 1 << 2 | 1 << 4; // 0b010101

        foreach (var direction in directions)
        {
            var hasWater = direction is HexDirection.Top or HexDirection.BottomRight or HexDirection.BottomLeft;
            var terrainType = hasWater ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeRawBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.ShouldBe(expectedMask);
    }

    [Fact]
    public void ComputeRawBitmask_NullNeighbor_DoesNotSetBit()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        // Top neighbor is null (out of bounds)
        var topCoords = centerCoords.GetNeighbour(HexDirection.Top);
        map.GetHex(topCoords).Returns((Hex?)null);

        // Other neighbors have water
        foreach (var direction in directions.Where(d => d != HexDirection.Top))
        {
            SetupNeighborsByBitmask(map, centerCoords, [direction], MakaMekTerrains.Water);
        }

        // Act
        var result = _sut.ComputeRawBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.ShouldBe((byte)0b111110); // Bits 1-5 set, bit 0 not set
    }

    [Fact]
    public void ComputeRawBitmask_NeighborWithoutRequestedTerrain_DoesNotSetBit()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        SetupNeighborsByBitmask(map, centerCoords, directions, MakaMekTerrains.LightWoods);

        // Act
        var result = _sut.ComputeRawBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.ShouldBe((byte)0);
    }

    [Fact]
    public void ComputeRawBitmask_DifferentTerrainType_DoesNotSetBits()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        SetupNeighborsByBitmask(map, centerCoords, directions, MakaMekTerrains.Water);

        // Act
        var result = _sut.ComputeRawBitmask(map, centerCoords, MakaMekTerrains.LightWoods);

        // Assert
        result.ShouldBe((byte)0);
    }

    [Fact]
    public void ComputeCanonicalBitmask_AllBitsZero_ReturnsZeroWithZeroRotation()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        SetupNeighborsByBitmask(map, centerCoords, directions, MakaMekTerrains.Clear);

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0);
        result.RotationSteps.ShouldBe(0);
    }

    [Fact]
    public void ComputeCanonicalBitmask_AllBitsSet_ReturnsAllBitsWithZeroRotation()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        SetupNeighborsByBitmask(map, centerCoords, directions, MakaMekTerrains.Water);

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0b111111);
        result.RotationSteps.ShouldBe(0);
    }

    [Fact]
    public void ComputeCanonicalBitmask_SingleBitAlreadyLowest_ReturnsSameMaskWithZeroRotation()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        foreach (var direction in directions)
        {
            var terrainType = direction == HexDirection.Top ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0b000001);
        result.RotationSteps.ShouldBe(0);
    }

    [Fact]
    public void ComputeCanonicalBitmask_SingleBitAtPosition2_RotatesToPosition0()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        foreach (var direction in directions)
        {
            var terrainType = direction == HexDirection.BottomRight ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0b000001);
        result.RotationSteps.ShouldBe(4); // Rotated 4 steps clockwise
    }

    [Fact]
    public void ComputeCanonicalBitmask_SingleBitAtTopLeft_RotatesToTop()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        foreach (var direction in directions)
        {
            var terrainType = direction == HexDirection.TopLeft ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        // Only TopLeft (position 5) has water (0b100000)
        // RotateMask formula: ((mask << steps) | (mask >> (6-steps))) & 0x3F
        // rot1: bit 5 wraps to bit 0 via the right-shift wrap: (0>>5)=1, so result = 0b000001
        // 0b000001 (1) is already the lowest value, so rotation 1 wins
        result.CanonicalMask.ShouldBe((byte)0b000001);
        result.RotationSteps.ShouldBe(1);
    }

    [Fact]
    public void ComputeCanonicalBitmask_AdjacentPairAlreadyCanonical_ReturnsSameWithZeroRotation()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        // Top (0) and TopRight (1) have water: 0b000011
        // This is already the lowest rotation
        foreach (var direction in directions)
        {
            var terrainType = direction is HexDirection.Top or HexDirection.TopRight ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0b000011);
        result.RotationSteps.ShouldBe(0);
    }

    [Fact]
    public void ComputeCanonicalBitmask_AdjacentPairNotCanonical_RotatesToLowest()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        // BottomRight (2) and Bottom (3) have water: 0b001100
        // rot1: 0b011000 (24), rot2: 0b110000 (48), rot3: 0b100001 (33), rot4: 0b000011 (3)
        // Lowest is 3 at rotation 4
        foreach (var direction in directions)
        {
            var terrainType = direction is HexDirection.BottomRight or HexDirection.Bottom ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0b000011);
        result.RotationSteps.ShouldBe(4);
    }

    [Fact]
    public void ComputeCanonicalBitmask_AlternatingPattern_ReturnsCorrectCanonical()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        // Positions 0, 2, 4 have water: 0b010101
        // This pattern has rotational symmetry - rotating by 2 gives the same pattern
        // 0b010101 (21), rot1: 0b101010 (42), rot2: 0b010101 (21), etc.
        // Lowest is 0b010101 (21) at rotation 0
        foreach (var direction in directions)
        {
            var terrainType = (int)direction % 2 == 0 ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0b010101);
        result.RotationSteps.ShouldBe(0);
    }

    [Fact]
    public void ComputeCanonicalBitmask_AlternatingPatternOffset_ReturnsCanonicalFromOffset()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        // Positions 1, 3, 5 have water: 0b101010
        // Should rotate 1 step to become 0b010101
        foreach (var direction in directions)
        {
            var terrainType = (int)direction % 2 == 1 ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0b010101);
        result.RotationSteps.ShouldBe(1);
    }

    [Fact]
    public void ComputeCanonicalBitmask_ThreeConsecutiveBits_ReturnsLowestRotation()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        // Positions 3, 4, 5 have water: 0b111000
        // rot1: 0b011100 (28), rot2: 0b001110 (14), rot3: 0b000111 (7)
        // Lowest is 7 at rotation 3
        foreach (var direction in directions)
        {
            var terrainType = direction is HexDirection.Bottom or HexDirection.BottomLeft or HexDirection.TopLeft ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0b000111);
        result.RotationSteps.ShouldBe(3);
    }

    [Fact]
    public void ComputeRawBitmask_AllNeighborsNull_ReturnsZero()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(0, 0);
        var directions = HexDirectionExtensions.AllDirections;

        // All neighbors are null (map boundaries)
        foreach (var direction in directions)
        {
            var neighborCoords = centerCoords.GetNeighbour(direction);
            map.GetHex(neighborCoords).Returns((Hex?)null);
        }

        // Act
        var result = _sut.ComputeRawBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.ShouldBe((byte)0);
    }

    [Fact]
    public void ComputeCanonicalBitmask_SingleBitAtVariousPositions_CanonicalizesCorrectly()
    {
        // Test that a single bit at any position canonicalizes to 0b000001
        for (var position = 0; position < 6; position++)
        {
            // Arrange
            var map = Substitute.For<IBattleMap>();
            var centerCoords = new HexCoordinates(5, 5);
            var directions = HexDirectionExtensions.AllDirections;

            foreach (var direction in directions)
            {
                var terrainType = (int)direction == position ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
                SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
            }

            // Act
            var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

            // Assert
            result.CanonicalMask.ShouldBe((byte)0b000001, $"Failed for position {position}");
            var expectedRotation = (6 - position) % 6;
            result.RotationSteps.ShouldBe(expectedRotation, $"Failed for position {position}");
        }
    }

    [Fact]
    public void ComputeCanonicalBitmask_TwoBitsWithGap_ReturnsLowestRotation()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        // Positions 0 and 3 have water (opposite): 0b001001
        // rot1: 0b100100 (36), rot2: 0b010010 (18), rot3: 0b001001 (9)
        // This has rotational symmetry of 3, lowest is 9 at rotation 0
        foreach (var direction in directions)
        {
            var terrainType = direction is HexDirection.Top or HexDirection.Bottom ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0b001001);
        result.RotationSteps.ShouldBe(0);
    }

    [Fact]
    public void ComputeCanonicalBitmask_TwoBitsAdjacentNotCanonical_RotatesCorrectly()
    {
        // Arrange
        var map = Substitute.For<IBattleMap>();
        var centerCoords = new HexCoordinates(3, 3);
        var directions = HexDirectionExtensions.AllDirections;

        // Positions 4 and 5 have water: 0b110000
        // rot1: (96|1)&63=33, rot2: (192|3)&63=3, rot3: 6, rot4: 12, rot5: 24
        // Lowest is 3 (0b000011) at rotation 2
        foreach (var direction in directions)
        {
            var terrainType = direction is HexDirection.BottomLeft or HexDirection.TopLeft ? MakaMekTerrains.Water : MakaMekTerrains.Clear;
            SetupNeighborsByBitmask(map, centerCoords, [direction], terrainType);
        }

        // Act
        var result = _sut.ComputeCanonicalBitmask(map, centerCoords, MakaMekTerrains.Water);

        // Assert
        result.CanonicalMask.ShouldBe((byte)0b000011);
        result.RotationSteps.ShouldBe(2);
    }

    private static Hex CreateHexWithTerrain(MakaMekTerrains terrainType)
    {
        var hex = new Hex(new HexCoordinates(0, 0));
        Terrain terrain = terrainType switch
        {
            MakaMekTerrains.Clear => new ClearTerrain(),
            MakaMekTerrains.LightWoods => new LightWoodsTerrain(),
            MakaMekTerrains.HeavyWoods => new HeavyWoodsTerrain(),
            MakaMekTerrains.Rough => new RoughTerrain(),
            MakaMekTerrains.Water => new WaterTerrain(),
            _ => throw new ArgumentOutOfRangeException(nameof(terrainType))
        };

        hex.AddTerrain(terrain);
        return hex;
    }
}
