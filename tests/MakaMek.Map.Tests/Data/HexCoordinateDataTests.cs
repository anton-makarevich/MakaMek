using Sanet.MakaMek.Map.Data;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Data;

public class HexCoordinateDataTests
{
    [Fact]
    public void ToString_ReturnsCorrectString()
    {
        // Arrange
        var sut = new HexCoordinateData(4, 6);
        
        // Act
        var result = sut.ToString();
        
        // Assert
        result.ShouldBe("0406");
    }
}