using Sanet.MakaMek.Map.Models;
using Sanet.MakaMek.Map.Models.Highlights;
using Shouldly;

namespace Sanet.MakaMek.Map.Tests.Models.Highlights;

public class LosBlockingHighlightTests
{
    [Fact]
    public void ShouldHaveCorrectRenderOrder()
    {
        // Arrange & Act
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.InterveningTerrain);

        // Assert
        sut.RenderOrder.ShouldBe(2);
    }

    [Fact]
    public void ShouldHaveCorrectName()
    {
        // Arrange & Act
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.InterveningTerrain);

        // Assert
        sut.Name.ShouldBe(nameof(LosBlockingHighlight));
    }

    [Fact]
    public void ShouldAcceptReasonInConstructor()
    {
        // Arrange & Act
        var sut = new LosBlockingHighlight(LineOfSightBlockReason.Elevation);

        // Assert
        sut.Reason.ShouldBe(LineOfSightBlockReason.Elevation);
    }
}
