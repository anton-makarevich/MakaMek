using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class LineOfSightResultTests
{
    [Fact]
    public void Blocked_WithHexPath_CreatesCorrectResult()
    {
        // Arrange
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(3, 3);
        var hexPath = new List<LineOfSightHexInfo>
        {
            new()
            {
                Hex = new Hex(new HexCoordinates(2,
                    2)),
                InterpolatedLosHeight = 1,
                InterveningFactorContribution = 2
            },
            new()
            {
                Hex = new Hex(new HexCoordinates(3,
                    3)),
                InterpolatedLosHeight = 2,
                InterveningFactorContribution = 3
            }
        };
        
        // Act
        var sut = LineOfSightResult.Blocked(from,
            to,
            new HexCoordinates(2,
                2),
            LineOfSightBlockReason.Elevation,
            1,
            1,
            hexPath);
        
        // Assert
        sut.HasLineOfSight.ShouldBeFalse();
        sut.From.ShouldBe(from);
        sut.To.ShouldBe(to);
        sut.BlockingHexCoordinates.ShouldBe(new HexCoordinates(2, 2));
        sut.BlockReason.ShouldBe(LineOfSightBlockReason.Elevation);
        sut.TotalInterveningFactor.ShouldBe(5);
        sut.HexPath.ShouldBeSameAs(hexPath);
    }
    
    [Fact]
    public void Unblocked_WithHexPath_CreatesCorrectResult()
    {
        // Arrange
        var from = new HexCoordinates(1, 1);
        var to = new HexCoordinates(3, 3);
        var hexPath = new List<LineOfSightHexInfo>
        {
            new()
            {
                Hex = new Hex(new HexCoordinates(2,
                    2)),
                InterpolatedLosHeight = 1,
                InterveningFactorContribution = 2
            },
            new()
            {
                Hex = new Hex(new HexCoordinates(3,
                    3)),
                InterpolatedLosHeight = 2,
                InterveningFactorContribution = 3
            }
        };
        
        // Act
        var sut = LineOfSightResult.Unblocked(from,
            to,
            1,
            1,
            hexPath);
        
        // Assert
        sut.HasLineOfSight.ShouldBeTrue();
        sut.From.ShouldBe(from);
        sut.To.ShouldBe(to);
        sut.BlockingHexCoordinates.ShouldBeNull();
        sut.BlockReason.ShouldBeNull();
        sut.TotalInterveningFactor.ShouldBe(5);
        sut.HexPath.ShouldBeSameAs(hexPath);
    }
    
    [Fact]
    public void Blocked_WithNullHexPath_DefaultsToEmptyPath()
    {
        var sut = LineOfSightResult.Blocked(
            new HexCoordinates(1, 1),
            new HexCoordinates(3, 3),
            new HexCoordinates(2, 2));
    
        sut.HexPath.ShouldBeEmpty();
        sut.TotalInterveningFactor.ShouldBe(0);
    }
}