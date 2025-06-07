using Sanet.MakaMek.Core.Models.Map;
using Shouldly;

namespace Sanet.MakaMek.Core.Tests.Models.Map;

public class HexDirectionTests
{
    [Theory]
    [InlineData(HexDirection.Top, HexDirection.Bottom)]
    [InlineData(HexDirection.TopRight, HexDirection.BottomLeft)]
    [InlineData(HexDirection.BottomRight, HexDirection.TopLeft)]
    [InlineData(HexDirection.Bottom, HexDirection.Top)]
    [InlineData(HexDirection.BottomLeft, HexDirection.TopRight)]
    [InlineData(HexDirection.TopLeft, HexDirection.BottomRight)]
    public void GetOppositeDirection_ReturnsCorrectOppositeDirection(HexDirection input, HexDirection expected)
    {
        // Act
        var result = input.GetOppositeDirection();

        // Assert
        result.ShouldBe(expected);
    }
    
    [Theory]
    [InlineData(HexDirection.Top, 0, HexDirection.Top)] // No rotation
    [InlineData(HexDirection.Top, 1, HexDirection.TopRight)] // Rotate 1 hexside clockwise
    [InlineData(HexDirection.Top, 2, HexDirection.BottomRight)] // Rotate 2 hexsides clockwise
    [InlineData(HexDirection.Top, 3, HexDirection.Bottom)] // Rotate 3 hexsides clockwise (opposite)
    [InlineData(HexDirection.Top, 4, HexDirection.BottomLeft)] // Rotate 4 hexsides clockwise
    [InlineData(HexDirection.Top, 5, HexDirection.TopLeft)] // Rotate 5 hexsides clockwise
    [InlineData(HexDirection.Top, 6, HexDirection.Top)] // Full rotation (back to original)
    [InlineData(HexDirection.TopRight, 2, HexDirection.Bottom)] // Starting from TopRight, rotate 2
    [InlineData(HexDirection.Bottom, 3, HexDirection.Top)] // Starting from Bottom, rotate 3
    [InlineData(HexDirection.Top, -1, HexDirection.TopLeft)] // Rotate 1 hexside counter-clockwise
    [InlineData(HexDirection.Top, -2, HexDirection.BottomLeft)] // Rotate 3 hexsides counter-clockwise
    [InlineData(HexDirection.Top, -3, HexDirection.Bottom)] // Starting from TopRight, rotate -2
    public void Rotate_ReturnsCorrectDirection(HexDirection startDirection, int hexsides, HexDirection expectedDirection)
    {
        // Act
        var result = startDirection.Rotate(hexsides);

        // Assert
        result.ShouldBe(expectedDirection);
    }
}
