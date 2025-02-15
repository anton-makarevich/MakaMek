using Shouldly;
using Sanet.MekForge.Core.Exceptions;
using Sanet.MekForge.Core.Models.Map;

namespace Sanet.MekForge.Core.Tests.Models.Map;

public class HexCoordinatesTests
{
    [Fact]
    public void Constructor_SetsQAndR()
    {
        // Arrange & Act
        var coords = new HexCoordinates(2, 3);

        // Assert
        coords.Q.ShouldBe(2);
        coords.R.ShouldBe(3);
    }

    [Fact]
    public void S_CalculatesCorrectly()
    {
        // Arrange
        var coords = new HexCoordinates(2, 3);

        // Act
        var s = coords.S;

        // Assert
        s.ShouldBe(-5); // -Q - R = -2 - 3 = -5
    }
    
    [Fact]
    public void CubeCoordinates_CalculateCorrectly()
    {
        // Arrange
        var hexEven = new HexCoordinates(2, 3); // Even column
        var hexOdd = new HexCoordinates(1, 2);  // Odd column

        // Assert
        hexEven.X.ShouldBe(2);
        hexEven.Z.ShouldBe(2); // Calculation: R - (Q + (Q % 2)) / 2 = 3 - (2 + 0) / 2
        hexEven.Y.ShouldBe(-4); // Calculation: -X - Z = -2 - 2

        hexOdd.X.ShouldBe(1);
        hexOdd.Z.ShouldBe(1); // Calculation: R - (Q + (Q % 2)) / 2 = 2 - (1 + 1) / 2
        hexOdd.Y.ShouldBe(-2); // Calculation: -X - Z = -1 - 1
    }
    
    [Theory]
    [InlineData(5, 4, 5, 3, HexDirection.Top)]
    [InlineData(5, 4, 6, 4, HexDirection.BottomRight)]
    [InlineData(5, 4, 4, 4, HexDirection.BottomLeft)]
    [InlineData(4, 4, 3, 4, HexDirection.TopLeft)]
    public void GetDirectionToNeighbour_ReturnsExpectedDirection(int centerQ, int centerR, int neighbourQ, int neighbourR, HexDirection expectedDirection)
    {
        // Arrange
        var center = new HexCoordinates(centerQ, centerR);
        var neighbour = new HexCoordinates(neighbourQ, neighbourR);

        // Act
        var direction = center.GetDirectionToNeighbour(neighbour);

        // Assert
        direction.ShouldBe(expectedDirection);
    }
    
    [Fact]
    public void GetDirectionToNeighbour_ThrowsWhenNotAdjacent()
    {
        // Arrange
        var center = new HexCoordinates(5, 4);
        var neighbour = new HexCoordinates(7, 4); // Not adjacent

        // Act & Assert
        Action act = () => center.GetDirectionToNeighbour(neighbour);
        Should.Throw<WrongHexException>(act)
            .Message.ShouldBe("Neighbour is not adjacent to center.");
    }

    [Theory]
    [InlineData(1, 1, 1, 2, 1)] // Adjacent hex
    [InlineData(1, 1, 2, 1, 1)] // Same row but shifted
    [InlineData(1, 1, 4, 4, 5)] // Larger distance
    [InlineData(1, 1, 1, 1, 0)] // Same hex
    [InlineData(2, 2, 4, 2, 2)] // Horizontal line on even row
    [InlineData(1, 1, 3, 3, 3)]
    [InlineData(2, 2, 2, 5, 3)]
    [InlineData(1, 1, 5, 5, 6)]
    [InlineData(5, 5, 1, 1, 6)]
    public void DistanceTo_ReturnsCorrectDistance(int q1, int r1, int q2, int r2, int expectedDistance)
    {
        // Arrange
        var hex1 = new HexCoordinates(q1, r1);
        var hex2 = new HexCoordinates(q2, r2);

        // Act
        var distance = hex1.DistanceTo(hex2);

        // Assert
        distance.ShouldBe(expectedDistance);
    }

    [Fact]
    public void DistanceTo_IsSymmetric()
    {
        // Arrange
        var hex1 = new HexCoordinates(2, -1);
        var hex2 = new HexCoordinates(-1, 2);

        // Act
        var distance1To2 = hex1.DistanceTo(hex2);
        var distance2To1 = hex2.DistanceTo(hex1);

        // Assert
        distance1To2.ShouldBe(distance2To1);
    }

    [Fact]
    public void GetAdjacentCoordinates_ReturnsAllSixNeighbors()
    {
        // Arrange
        var center = new HexCoordinates(2, 2);

        // Act
        var neighbors = center.GetAdjacentCoordinates().ToList();

        // Assert
        neighbors.Count.ShouldBe(6);
        neighbors.ShouldContain(new HexCoordinates(1, 2));   // East
        neighbors.ShouldContain(new HexCoordinates(1, 3));  // Northeast
        neighbors.ShouldContain(new HexCoordinates(2, 1));  // Northwest
        neighbors.ShouldContain(new HexCoordinates(2, 3));  // West
        neighbors.ShouldContain(new HexCoordinates(3, 2));  // Southwest
        neighbors.ShouldContain(new HexCoordinates(3, 3));   // Southeast
    }

    [Fact]
    public void GetAdjacentCoordinates_AllNeighborsAreDistanceOne()
    {
        // Arrange
        var center = new HexCoordinates(2, 3); // Using non-zero coordinates

        // Act
        var neighbors = center.GetAdjacentCoordinates();

        // Assert
        foreach (var neighbor in neighbors)
        {
            center.DistanceTo(neighbor).ShouldBe(1);
        }
    }

    [Theory]
    [InlineData(2, 2, 1)]  // Range 1 from origin
    [InlineData(3, 3, 1)]  // Range 1 from origin
    [InlineData(2, 2, 2)]  // Range 2 from origin
    [InlineData(3, 3, 2)]  // Range 2 from origin
    public void GetCoordinatesInRange_ReturnsCorrectHexes(int centerQ, int centerR, int range)
    {
        // Arrange
        var center = new HexCoordinates(centerQ, centerR);

        // Act
        var hexesInRange = center.GetCoordinatesInRange(range).ToList();

        // Assert
        foreach (var hex in hexesInRange)
        {
            center.DistanceTo(hex).ShouldBeLessThanOrEqualTo(range);
        }

        // Verify that all hexes at exactly range distance are included
        var hexesAtRange = hexesInRange.Where(h => center.DistanceTo(h) == range);
        hexesAtRange.Count().ShouldBe(6 * range); // Each range adds 6 more hexes
    }

    [Fact]
    public void X_CalculatesCorrectPixelPosition()
    {
        // Arrange & Act
        var hex1 = new HexCoordinates(0, 0);
        var hex2 = new HexCoordinates(1, 0);
        var hex3 = new HexCoordinates(2, 0);

        // Assert
        hex1.H.ShouldBe(0);
        hex2.H.ShouldBe(75); // 100 * 0.75
        hex3.H.ShouldBe(150); // 200 * 0.75
    }

    [Fact]
    public void Y_CalculatesCorrectPixelPosition()
    {
        // Arrange & Act
        var hex1 = new HexCoordinates(0, 0); // Even Q
        var hex2 = new HexCoordinates(0, 1); // Even Q
        var hex3 = new HexCoordinates(1, 0); // Odd Q
        var hex4 = new HexCoordinates(1, 1); // Odd Q

        // Assert
        hex1.V.ShouldBe(0);
        hex2.V.ShouldBe(HexCoordinates.HexHeight);
        hex3.V.ShouldBe( -HexCoordinates.HexHeight*0.5);  // Offset for odd Q
        hex4.V.ShouldBe(HexCoordinates.HexHeight*0.5);  // Height - 0.5*Height offset for odd Q
    }
    
    [Fact]
    public void LineTo_ShouldReturnHexes_WhenClearPath()
    {
        // Arrange
        var start = new HexCoordinates(1, 1);
        var end = new HexCoordinates(3, 3);

        // Act
        var hexes = start.LineTo(end).Select(s => s.MainOption).ToList();

        // Assert
        hexes.ShouldNotBeNull();
        hexes.Count.ShouldBe(4);
        hexes.ShouldBe(new[] { new HexCoordinates(1, 1), new HexCoordinates(2,1), new HexCoordinates(2, 2), new HexCoordinates(3, 3) });
    }

    [Fact]
    public void LineTo_ShouldHandleSameHex()
    {
        // Arrange
        var coordinates = new HexCoordinates(2, 2);
        
        // Act
        var hexes = coordinates.LineTo(coordinates).Select(s => s.MainOption).ToList();

        // Assert
        hexes.ShouldNotBeNull();
        hexes.Count.ShouldBe(1);
        hexes.ShouldBe(new[] { coordinates });
    }
    
    [Fact]
    public void ToData_ReturnsCorrectDataObject()
    {
        // Arrange
        var hexCoordinates = new HexCoordinates(3, 4);
         
        // Act
        var data = hexCoordinates.ToData();
         
        // Assert
        data.Q.ShouldBe(3);
        data.R.ShouldBe(4);
    }

    [Fact]
    public void ToString_ReturnsHexCoordinatesAsText()
    {
        // Arrange
        var hexCoordinates = new HexCoordinates(3, 4);
        
        // Act
        var text = hexCoordinates.ToString();
        
        // Assert
        text.ShouldBe("0304");
    }

    [Fact]
    public void GetHexesInFiringArc_ForwardArcRange2_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(5, 5);
        var expectedHexes = new[]
        {
            // Range 1 (3 hexes)
            new HexCoordinates(4, 4),
            new HexCoordinates(5, 4),
            new HexCoordinates(6, 4),
            
            // Range 2 (5 hexes)
            new HexCoordinates(3, 4),
            new HexCoordinates(4, 3),
            new HexCoordinates(5, 3),
            new HexCoordinates(6, 3),
            new HexCoordinates(7, 4)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.Top, FiringArc.Forward, 2).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(8);
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RearArcRange2_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(5, 5);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(5, 6),
            
            // Range 2 (3 hexes)
            new HexCoordinates(4, 6),
            new HexCoordinates(5, 7),
            new HexCoordinates(6, 6)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.Top, FiringArc.Rear, 2).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(4);
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_ForwardArcRange2_FacingBottomRight_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(5, 5);
        var expectedHexes = new[]
        {
            // Range 1 (3 hexes)
            new HexCoordinates(6, 4),
            new HexCoordinates(6, 5),
            new HexCoordinates(5, 6),
            
            // Range 2 (5 hexes)
            new HexCoordinates(7, 5),
            new HexCoordinates(7, 6),
            new HexCoordinates(7, 4),
            new HexCoordinates(5, 7),
            new HexCoordinates(6, 6)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.BottomRight, FiringArc.Forward, 2).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(8);
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RearArcRange2_FacingBottomRight_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(5, 5);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(4, 4),
            
            // Range 2 (3 hexes)
            new HexCoordinates(4, 3),
            new HexCoordinates(3, 4),
            new HexCoordinates(3, 5)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.BottomRight, FiringArc.Rear, 2).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(4);
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_ForwardArcRange2_OddColumn_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 5);
        var expectedHexes = new[]
        {
            // Range 1 (3 hexes)
            new HexCoordinates(5, 5),
            new HexCoordinates(6, 4),
            new HexCoordinates(7, 5),
            
            // Range 2 (5 hexes)
            new HexCoordinates(4, 4),
            new HexCoordinates(5, 4),
            new HexCoordinates(6, 3),
            new HexCoordinates(7, 4),
            new HexCoordinates(8, 4)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.Top, FiringArc.Forward, 2).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(8);
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RearArcRange2_OddColumn_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 5);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(6, 6),
            
            // Range 2 (3 hexes)
            new HexCoordinates(5, 7),
            new HexCoordinates(6, 7),
            new HexCoordinates(7, 7)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.Top, FiringArc.Rear, 2).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(4);
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_ForwardArcRange5_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (3 hexes)
            new HexCoordinates(5, 6),
            new HexCoordinates(6, 5),
            new HexCoordinates(7, 6),
            
            // Range 2 (5 hexes)
            new HexCoordinates(4, 5),
            new HexCoordinates(5, 5),
            new HexCoordinates(6, 4),
            new HexCoordinates(7, 5),
            new HexCoordinates(8, 5),

            // Range 3 (7 hexes)
            new HexCoordinates(3, 5),
            new HexCoordinates(4, 4),
            new HexCoordinates(5, 4),
            new HexCoordinates(6, 3),
            new HexCoordinates(7, 4),
            new HexCoordinates(8, 4),
            new HexCoordinates(9, 5),

            // Range 4 (9 hexes)
            new HexCoordinates(2, 4),
            new HexCoordinates(3, 4),
            new HexCoordinates(4, 3),
            new HexCoordinates(5, 3),
            new HexCoordinates(6, 2),
            new HexCoordinates(7, 3),
            new HexCoordinates(8, 3),
            new HexCoordinates(9, 4),
            new HexCoordinates(10, 4),

            // Range 5 (11 hexes)
            new HexCoordinates(1, 4),
            new HexCoordinates(2, 3),
            new HexCoordinates(3, 3),
            new HexCoordinates(4, 2),
            new HexCoordinates(5, 2),
            new HexCoordinates(6, 1),
            new HexCoordinates(7, 2),
            new HexCoordinates(8, 2),
            new HexCoordinates(9, 3),
            new HexCoordinates(10, 3),
            new HexCoordinates(11, 4)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.Top, FiringArc.Forward, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(35); // 3 + 5 + 7 + 9 + 11 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RearArcRange5_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(6, 7),
            
            // Range 2 (3 hexes)
            new HexCoordinates(5, 8),
            new HexCoordinates(6, 8),
            new HexCoordinates(7, 8),

            // Range 3 (5 hexes)
            new HexCoordinates(4, 8),
            new HexCoordinates(5, 9),
            new HexCoordinates(6, 9),
            new HexCoordinates(7, 9),
            new HexCoordinates(8, 8),

            // Range 4 (7 hexes)
            new HexCoordinates(3, 9),
            new HexCoordinates(4, 9),
            new HexCoordinates(5, 10),
            new HexCoordinates(6, 10),
            new HexCoordinates(7, 10),
            new HexCoordinates(8, 9),
            new HexCoordinates(9, 9),

            // Range 5 (9 hexes)
            new HexCoordinates(2, 9),
            new HexCoordinates(3, 10),
            new HexCoordinates(4, 10),
            new HexCoordinates(5, 11),
            new HexCoordinates(6, 11),
            new HexCoordinates(7, 11),
            new HexCoordinates(8, 10),
            new HexCoordinates(9, 10),
            new HexCoordinates(10, 9)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.Top, FiringArc.Rear, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(25); // 1 + 3 + 5 + 7 + 9 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_ForwardArcRange5_FacingTopRight_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (3 hexes)
            new HexCoordinates(7, 7),
            new HexCoordinates(7, 6),
            new HexCoordinates(6, 5),
            
            // Range 2 (5 hexes)
            new HexCoordinates(8, 5),
            new HexCoordinates(8, 6),
            new HexCoordinates(8, 7),
            new HexCoordinates(6, 4),
            new HexCoordinates(7, 5),

            // Range 3 (7 hexes)
            new HexCoordinates(9, 5),
            new HexCoordinates(9, 6),
            new HexCoordinates(8, 4),
            new HexCoordinates(7, 4),
            new HexCoordinates(6, 3),
            new HexCoordinates(9, 7),
            new HexCoordinates(9, 8),

            // Range 4 (9 hexes)
            new HexCoordinates(6,2),
            new HexCoordinates(7,3),
            new HexCoordinates(8,3),
            new HexCoordinates(9,4),
            new HexCoordinates(10, 4),
            new HexCoordinates(10, 5),
            new HexCoordinates(10, 6),
            new HexCoordinates(10, 7),
            new HexCoordinates(10, 8),

            // Range 5 (11 hexes)
            new HexCoordinates(6, 1),
            new HexCoordinates(7, 2),
            new HexCoordinates(8, 2),
            new HexCoordinates(9, 3),
            new HexCoordinates(10, 3),
            new HexCoordinates(11, 4),
            new HexCoordinates(11, 5),
            new HexCoordinates(11, 6),
            new HexCoordinates(11, 7),
            new HexCoordinates(11, 8),
            new HexCoordinates(11, 9)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.TopRight, FiringArc.Forward, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(35); // 3 + 5 + 7 + 9 + 11 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RearArcRange5_FacingTopRight_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(5, 7),
            
            // Range 2 (3 hexes)
            new HexCoordinates(4, 7),
            new HexCoordinates(5, 8),
            new HexCoordinates(4, 6),

            // Range 3 (5 hexes)
            new HexCoordinates(3, 6),
            new HexCoordinates(3, 7),
            new HexCoordinates(3, 8),
            new HexCoordinates(4, 8),
            new HexCoordinates(5, 9),

            // Range 4 (7 hexes)
            new HexCoordinates(2, 5),
            new HexCoordinates(2, 6),
            new HexCoordinates(2, 7),
            new HexCoordinates(2, 8),
            new HexCoordinates(3, 9),
            new HexCoordinates(4, 9),
            new HexCoordinates(5, 10),

            // Range 5 (9 hexes)
            new HexCoordinates(1, 5),
            new HexCoordinates(1, 6),
            new HexCoordinates(1, 7),
            new HexCoordinates(1, 8),
            new HexCoordinates(1, 9),
            new HexCoordinates(2, 9),
            new HexCoordinates(3, 10),
            new HexCoordinates(4, 10),
            new HexCoordinates(5, 11)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.TopRight, FiringArc.Rear, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(25); // 1 + 3 + 5 + 7 + 9 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_ForwardArcRange5_FacingBottomRight_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (3 hexes)
            new HexCoordinates(7, 6),
            new HexCoordinates(7, 7),
            new HexCoordinates(6, 7),
            
            // Range 2 (5 hexes)
            new HexCoordinates(8, 6),
            new HexCoordinates(8, 7),
            new HexCoordinates(8, 5),
            new HexCoordinates(7, 8),
            new HexCoordinates(6, 8),

            // Range 3 (7 hexes)
            new HexCoordinates(9, 6),
            new HexCoordinates(9, 7),
            new HexCoordinates(9, 8),
            new HexCoordinates(9, 5),
            new HexCoordinates(8, 8),
            new HexCoordinates(7, 9),
            new HexCoordinates(6, 9),

            // Range 4 (9 hexes)
            new HexCoordinates(10, 6),
            new HexCoordinates(10, 7),
            new HexCoordinates(10, 8),
            new HexCoordinates(10, 4),
            new HexCoordinates(10, 5),
            new HexCoordinates(9, 9),
            new HexCoordinates(8, 9),
            new HexCoordinates(7, 10),
            new HexCoordinates(6, 10),

            // Range 5 (11 hexes)
            new HexCoordinates(11, 6),
            new HexCoordinates(11, 7),
            new HexCoordinates(11, 8),
            new HexCoordinates(11, 9),
            new HexCoordinates(11, 4),
            new HexCoordinates(11, 5),
            new HexCoordinates(10, 9),
            new HexCoordinates(9, 10),
            new HexCoordinates(8, 10),
            new HexCoordinates(7, 11),
            new HexCoordinates(6, 11)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.BottomRight, FiringArc.Forward, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(35); // 3 + 5 + 7 + 9 + 11 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RearArcRange5_FacingBottomRight_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(5, 6),
            
            // Range 2 (3 hexes)
            new HexCoordinates(4, 5),
            new HexCoordinates(4, 6),
            new HexCoordinates(5, 5),

            // Range 3 (5 hexes)
            new HexCoordinates(3, 5),
            new HexCoordinates(3, 6),
            new HexCoordinates(3, 7),
            new HexCoordinates(4, 4),
            new HexCoordinates(5, 4),

            // Range 4 (7 hexes)
            new HexCoordinates(2, 5),
            new HexCoordinates(2, 4),
            new HexCoordinates(2, 6),
            new HexCoordinates(2, 7),
            new HexCoordinates(3, 4),
            new HexCoordinates(4, 3),
            new HexCoordinates(5, 3),

            // Range 5 (9 hexes)
            new HexCoordinates(1, 5),
            new HexCoordinates(1, 4),
            new HexCoordinates(1, 6),
            new HexCoordinates(1, 7),
            new HexCoordinates(1, 8),
            new HexCoordinates(2, 3),
            new HexCoordinates(3, 3),
            new HexCoordinates(4, 2),
            new HexCoordinates(5, 2)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.BottomRight, FiringArc.Rear, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(25); // 1 + 3 + 5 + 7 + 9 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_ForwardArcRange5_FacingBottom_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (3 hexes)
            new HexCoordinates(5, 7),
            new HexCoordinates(6, 7),
            new HexCoordinates(7, 7),
            
            // Range 2 (5 hexes)
            new HexCoordinates(4, 7),
            new HexCoordinates(5, 8),
            new HexCoordinates(6, 8),
            new HexCoordinates(7, 8),
            new HexCoordinates(8, 7),

            // Range 3 (7 hexes)
            new HexCoordinates(3, 8),
            new HexCoordinates(4, 8),
            new HexCoordinates(5, 9),
            new HexCoordinates(6, 9),
            new HexCoordinates(7, 9),
            new HexCoordinates(8, 8),
            new HexCoordinates(9, 8),

            // Range 4 (9 hexes)
            new HexCoordinates(2, 8),
            new HexCoordinates(3, 9),
            new HexCoordinates(4, 9),
            new HexCoordinates(5, 10),
            new HexCoordinates(6, 10),
            new HexCoordinates(7, 10),
            new HexCoordinates(8, 9),
            new HexCoordinates(9, 9),
            new HexCoordinates(10, 8),

            // Range 5 (11 hexes)
            new HexCoordinates(1, 9),
            new HexCoordinates(2, 9),
            new HexCoordinates(3, 10),
            new HexCoordinates(4, 10),
            new HexCoordinates(5, 11),
            new HexCoordinates(6, 11),
            new HexCoordinates(7, 11),
            new HexCoordinates(8, 10),
            new HexCoordinates(9, 10),
            new HexCoordinates(10, 9),
            new HexCoordinates(11, 9)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.Bottom, FiringArc.Forward, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(35); // 3 + 5 + 7 + 9 + 11 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RearArcRange5_FacingBottom_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(6, 5),
            
            // Range 2 (3 hexes)
            new HexCoordinates(5, 5),
            new HexCoordinates(6, 4),
            new HexCoordinates(7, 5),

            // Range 3 (5 hexes)
            new HexCoordinates(4, 4),
            new HexCoordinates(5, 4),
            new HexCoordinates(6, 3),
            new HexCoordinates(7, 4),
            new HexCoordinates(8, 4),

            // Range 4 (7 hexes)
            new HexCoordinates(3, 4),
            new HexCoordinates(4, 3),
            new HexCoordinates(5, 3),
            new HexCoordinates(6, 2),
            new HexCoordinates(7, 3),
            new HexCoordinates(8, 3),
            new HexCoordinates(9, 4),

            // Range 5 (9 hexes)
            new HexCoordinates(2, 3),
            new HexCoordinates(3, 3),
            new HexCoordinates(4, 2),
            new HexCoordinates(5, 2),
            new HexCoordinates(6, 1),
            new HexCoordinates(7, 2),
            new HexCoordinates(8, 2),
            new HexCoordinates(9, 3),
            new HexCoordinates(10, 3)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.Bottom, FiringArc.Rear, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(25); // 1 + 3 + 5 + 7 + 9 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_ForwardArcRange5_FacingBottomLeft_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (3 hexes)
            new HexCoordinates(5, 7),
            new HexCoordinates(5, 6),
            new HexCoordinates(6, 7),
            
            // Range 2 (5 hexes)
            new HexCoordinates(4, 5),
            new HexCoordinates(4, 6),
            new HexCoordinates(4, 7),
            new HexCoordinates(5, 8),
            new HexCoordinates(6, 8),

            // Range 3 (7 hexes)
            new HexCoordinates(3, 5),
            new HexCoordinates(3, 6),
            new HexCoordinates(3, 7),
            new HexCoordinates(3, 8),
            new HexCoordinates(4, 8),
            new HexCoordinates(5, 9),
            new HexCoordinates(6, 9),

            // Range 4 (9 hexes)
            new HexCoordinates(2, 4),
            new HexCoordinates(2, 5),
            new HexCoordinates(2, 6),
            new HexCoordinates(2, 7),
            new HexCoordinates(2, 8),
            new HexCoordinates(3, 9),
            new HexCoordinates(4, 9),
            new HexCoordinates(5, 10),
            new HexCoordinates(6, 10),

            // Range 5 (11 hexes)
            new HexCoordinates(1, 4),
            new HexCoordinates(1, 5),
            new HexCoordinates(1, 6),
            new HexCoordinates(1, 7),
            new HexCoordinates(1, 8),
            new HexCoordinates(1, 9),
            new HexCoordinates(2, 9),
            new HexCoordinates(3, 10),
            new HexCoordinates(4, 10),
            new HexCoordinates(5, 11),
            new HexCoordinates(6, 11)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.BottomLeft, FiringArc.Forward, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(35); // 3 + 5 + 7 + 9 + 11 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RearArcRange5_FacingBottomLeft_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(7, 6),
            
            // Range 2 (3 hexes)
            new HexCoordinates(8, 5),
            new HexCoordinates(8, 6),
            new HexCoordinates(7, 5),

            // Range 3 (5 hexes)
            new HexCoordinates(9, 5),
            new HexCoordinates(9, 6),
            new HexCoordinates(9, 7),
            new HexCoordinates(8, 4),
            new HexCoordinates(7, 4),

            // Range 4 (7 hexes)
            new HexCoordinates(10, 5),
            new HexCoordinates(10, 4),
            new HexCoordinates(10, 6),
            new HexCoordinates(10, 7),
            new HexCoordinates(9, 4),
            new HexCoordinates(8, 3),
            new HexCoordinates(7, 3),

            // Range 5 (9 hexes)
            new HexCoordinates(11, 8),
            new HexCoordinates(11, 7),
            new HexCoordinates(11, 6),
            new HexCoordinates(11, 5),
            new HexCoordinates(11, 4),
            new HexCoordinates(10, 3),
            new HexCoordinates(9, 3),
            new HexCoordinates(8, 2),
            new HexCoordinates(7, 2)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.BottomLeft, FiringArc.Rear, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(25); // 1 + 3 + 5 + 7 + 9 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_ForwardArcRange5_FacingTopLeft_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (3 hexes)
            new HexCoordinates(5, 7),
            new HexCoordinates(5, 6),
            new HexCoordinates(6, 5),
            
            // Range 2 (5 hexes)
            new HexCoordinates(4, 6),
            new HexCoordinates(4, 5),
            new HexCoordinates(4, 7),
            new HexCoordinates(5, 5),
            new HexCoordinates(6, 4),

            // Range 3 (7 hexes)
            new HexCoordinates(3, 8),
            new HexCoordinates(3, 7),
            new HexCoordinates(3, 6),
            new HexCoordinates(3, 5),
            new HexCoordinates(4, 4),
            new HexCoordinates(5, 4),
            new HexCoordinates(6, 3),

            // Range 4 (9 hexes)
            new HexCoordinates(2, 8),
            new HexCoordinates(2, 7),
            new HexCoordinates(2, 6),
            new HexCoordinates(2, 5),
            new HexCoordinates(2, 4),
            new HexCoordinates(3, 4),
            new HexCoordinates(4, 3),
            new HexCoordinates(5, 3),
            new HexCoordinates(6, 2),

            // Range 5 (11 hexes)
            new HexCoordinates(1, 9),
            new HexCoordinates(1, 8),
            new HexCoordinates(1, 7),
            new HexCoordinates(1, 6),
            new HexCoordinates(1, 5),
            new HexCoordinates(1, 4),
            new HexCoordinates(2, 3),
            new HexCoordinates(3, 3),
            new HexCoordinates(4, 2),
            new HexCoordinates(5, 2),
            new HexCoordinates(6, 1)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.TopLeft, FiringArc.Forward, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(35); // 3 + 5 + 7 + 9 + 11 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RearArcRange5_FacingTopLeft_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(7, 7),
            
            // Range 2 (3 hexes)
            new HexCoordinates(8, 7),
            new HexCoordinates(8, 6),
            new HexCoordinates(7, 8),

            // Range 3 (5 hexes)
            new HexCoordinates(9, 7),
            new HexCoordinates(9, 8),
            new HexCoordinates(9, 6),
            new HexCoordinates(8, 8),
            new HexCoordinates(7, 9),

            // Range 4 (7 hexes)
            new HexCoordinates(10, 5),
            new HexCoordinates(10, 6),
            new HexCoordinates(10, 7),
            new HexCoordinates(10, 8),
            new HexCoordinates(9, 9),
            new HexCoordinates(8, 9),
            new HexCoordinates(7, 10),

            // Range 5 (9 hexes)
            new HexCoordinates(11, 5),
            new HexCoordinates(11, 6),
            new HexCoordinates(11, 7),
            new HexCoordinates(11, 8),
            new HexCoordinates(11, 9),
            new HexCoordinates(10, 09),
            new HexCoordinates(9, 10),
            new HexCoordinates(8, 10),
            new HexCoordinates(7, 11)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.TopLeft, FiringArc.Rear, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(25); // 1 + 3 + 5 + 7 + 9 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_LeftArcRange5_FacingTop_ReturnsCorrectHexes()
    {
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(5, 7),
            
            // Range 2 (2 hexes)
            new HexCoordinates(4, 6),
            new HexCoordinates(4, 7),

            // Range 3 (3 hexes)
            new HexCoordinates(3, 6),
            new HexCoordinates(3, 7),
            new HexCoordinates(3, 8),

            // Range 4 (4 hexes)
            new HexCoordinates(2, 5),
            new HexCoordinates(2, 6),
            new HexCoordinates(2, 7),
            new HexCoordinates(2, 8),

            // Range 5 (5 hexes)
            new HexCoordinates(1, 6),
            new HexCoordinates(1, 7),
            new HexCoordinates(1, 8),
            new HexCoordinates(1, 9),
            new HexCoordinates(1, 5)
        };
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.Top, FiringArc.Left, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(15); // 1 + 2 + 3 + 4 + 5 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RightArcRange5_FacingTop_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(7, 7),
            
            // Range 2 (2 hexes)
            new HexCoordinates(8, 6),
            new HexCoordinates(8, 7),

            // Range 3 (3 hexes)
            new HexCoordinates(9, 7),
            new HexCoordinates(9, 8),
            new HexCoordinates(9, 6),

            // Range 4 (4 hexes)
            new HexCoordinates(10, 7),
            new HexCoordinates(10, 8),
            new HexCoordinates(10, 6),
            new HexCoordinates(10, 5),

            // Range 5 (5 hexes)
            new HexCoordinates(11, 7),
            new HexCoordinates(11, 8),
            new HexCoordinates(11, 9),
            new HexCoordinates(11, 6),
            new HexCoordinates(11, 5)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.Top, FiringArc.Right, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(15); // 1 + 2 + 3 + 4 + 5 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_LeftArcRange5_FacingBottomRight_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(6, 5),
            
            // Range 2 (2 hexes)
            new HexCoordinates(6, 4),
            new HexCoordinates(7, 5),

            // Range 3 (3 hexes)
            new HexCoordinates(6, 3),
            new HexCoordinates(7, 4),
            new HexCoordinates(8, 4),

            // Range 4 (4 hexes)
            new HexCoordinates(6, 2),
            new HexCoordinates(7, 3),
            new HexCoordinates(8, 3),
            new HexCoordinates(9, 4),

            // Range 5 (5 hexes)
            new HexCoordinates(6, 1),
            new HexCoordinates(7, 2),
            new HexCoordinates(8, 2),
            new HexCoordinates(9, 3),
            new HexCoordinates(10, 3)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.BottomRight, FiringArc.Left, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(15); // 1 + 2 + 3 + 4 + 5 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void GetHexesInFiringArc_RightArcRange5_FacingBottomRight_ReturnsCorrectHexes()
    {
        // Arrange
        var unitPosition = new HexCoordinates(6, 6);
        var expectedHexes = new[]
        {
            // Range 1 (1 hex)
            new HexCoordinates(5, 7),
            
            // Range 2 (2 hexes)
            new HexCoordinates(4, 7),
            new HexCoordinates(5, 8),

            // Range 3 (3 hexes)
            new HexCoordinates(3, 8),
            new HexCoordinates(4, 8),
            new HexCoordinates(5, 9),

            // Range 4 (4 hexes)
            new HexCoordinates(2, 8),
            new HexCoordinates(3, 9),
            new HexCoordinates(4, 9),
            new HexCoordinates(5, 10),

            // Range 5 (5 hexes)
            new HexCoordinates(1, 9),
            new HexCoordinates(2, 9),
            new HexCoordinates(3, 10),
            new HexCoordinates(4, 10),
            new HexCoordinates(5, 11)
        };

        // Act
        var hexesInArc = unitPosition.GetHexesInFiringArc(HexDirection.BottomRight, FiringArc.Right, 5).ToList();

        // Assert
        hexesInArc.Count.ShouldBe(15); // 1 + 2 + 3 + 4 + 5 hexes
        hexesInArc.ShouldBe(expectedHexes, true);
    }

    [Fact]
    public void LineTo_HorizontalLine_WithDividedSegments()
    {
        // Arrange
        var start = new HexCoordinates(2, 2);
        var end = new HexCoordinates(6, 2);
        
        // Act
        var segments = start.LineTo(end);

        // Assert
        segments.Count.ShouldBe(5);
        
        // {(2,2)}
        segments[0].MainOption.ShouldBe(new HexCoordinates(2, 2));
        segments[0].SecondOption.ShouldBeNull();
        
        // {(3,2)(3,3)}
        segments[1].MainOption.ShouldBe(new HexCoordinates(3, 2));
        segments[1].SecondOption.ShouldBe(new HexCoordinates(3, 3));
        
        // {(4,2)}
        segments[2].MainOption.ShouldBe(new HexCoordinates(4, 2));
        segments[2].SecondOption.ShouldBeNull();
        
        // {(5,2)(5,3)}
        segments[3].MainOption.ShouldBe(new HexCoordinates(5, 2));
        segments[3].SecondOption.ShouldBe(new HexCoordinates(5, 3));
        
        // {(6,2)}
        segments[4].MainOption.ShouldBe(new HexCoordinates(6, 2));
        segments[4].SecondOption.ShouldBeNull();
    }

    [Fact]
    public void LineTo_ShouldReturnCorrectHexSequence()
    {
        // Arrange
        var start = new HexCoordinates(2, 3);
        var end = new HexCoordinates(7, 3);
        var expectedSequence = new[]
        {
            new HexCoordinates(2, 3),
            new HexCoordinates(3, 3),
            new HexCoordinates(4, 3),
            new HexCoordinates(5, 3),
            new HexCoordinates(6, 3),
            new HexCoordinates(7, 3)
        };

        // Act
        var actualSequence = start.LineTo(end).Select(s => s.MainOption).ToList();

        // Assert
        actualSequence.ShouldBe(expectedSequence,true);
    }

    [Fact]
    public void LineTo_ShouldReturnCorrectHexSequence2()
    {
        // Arrange
        var start = new HexCoordinates(6, 7);
        var end = new HexCoordinates(10, 6);
        var expectedSequence = new[]
        {
            new HexCoordinates(6, 7),
            new HexCoordinates(7, 7),
            new HexCoordinates(8, 7),
            new HexCoordinates(8, 6),
            new HexCoordinates(9, 7),
            new HexCoordinates(10,6)
        };

        // Act
        var lineSegments = start.LineTo(end);
        var actualSequence = lineSegments.Select(s => s.MainOption).ToList();

        // Assert
        actualSequence.ShouldBe(expectedSequence,true);
    }
    
    [Fact]
    public void LineTo_ShouldReturnCorrectHexSequence3()
    {
        // Arrange
        var start = new HexCoordinates(7, 10);
        var end = new HexCoordinates(5, 17);

        // Act
        var actualSequence = start.LineTo(end).Select(s => s.MainOption).ToList();

        // Assert
        actualSequence.ShouldContain(new HexCoordinates(6,11));
    }
}
