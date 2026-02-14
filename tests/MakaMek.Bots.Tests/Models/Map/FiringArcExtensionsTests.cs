using Sanet.MakaMek.Bots.Models.Map;
using Sanet.MakaMek.Core.Models.Map;
using Sanet.MakaMek.Map.Models;
using Shouldly;

namespace Sanet.MakaMek.Bots.Tests.Models.Map;

public class FiringArcExtensionsTests
{
    [Theory]
    [InlineData(FiringArc.Front, 1.0)]
    [InlineData(FiringArc.Left, 1.5)]
    [InlineData(FiringArc.Right, 1.5)]
    [InlineData(FiringArc.Rear, 2.0)]
    public void GetArcMultiplier_ReturnsCorrectValue(FiringArc arc, double expectedMultiplier)
    {
        // Act
        var result = arc.GetArcMultiplier();

        // Assert
        result.ShouldBe(expectedMultiplier);
    }
}