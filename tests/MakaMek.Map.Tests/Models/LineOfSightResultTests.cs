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
        var result = LineOfSightResult.Blocked(from,
            to,
            new HexCoordinates(2,
                2),
            LineOfSightBlockReason.Elevation,
            1,
            1,
            hexPath);
        
        // Assert
        result.HasLineOfSight.ShouldBeFalse();
        result.From.ShouldBe(from);
        result.To.ShouldBe(to);
        result.BlockingHexCoordinates.ShouldBe(new HexCoordinates(2, 2));
        result.BlockReason.ShouldBe(LineOfSightBlockReason.Elevation);
        result.TotalInterveningFactor.ShouldBe(5);
        result.HexPath.ShouldBeSameAs(hexPath);
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
        var result = LineOfSightResult.Unblocked(from,
            to,
            1,
            1,
            hexPath);
        
        // Assert
        result.HasLineOfSight.ShouldBeTrue();
        result.From.ShouldBe(from);
        result.To.ShouldBe(to);
        result.BlockingHexCoordinates.ShouldBeNull();
        result.BlockReason.ShouldBeNull();
        result.TotalInterveningFactor.ShouldBe(5);
        result.HexPath.ShouldBeSameAs(hexPath);
    }
}