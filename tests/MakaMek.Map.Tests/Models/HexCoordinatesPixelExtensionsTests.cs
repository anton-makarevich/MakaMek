using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models;

public class HexCoordinatesPixelExtensionsTests
{
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
        hex2.V.ShouldBe(HexCoordinatesPixelExtensions.HexHeight);
        hex3.V.ShouldBe( -HexCoordinatesPixelExtensions.HexHeight*0.5);  // Offset for odd Q
        hex4.V.ShouldBe(HexCoordinatesPixelExtensions.HexHeight*0.5);  // Height - 0.5*Height offset for odd Q
    }
}